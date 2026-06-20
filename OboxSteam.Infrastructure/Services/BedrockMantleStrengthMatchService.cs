using Microsoft.Extensions.Logging;
using OpenAI;
using OpenAI.Chat;
using OboxSteam.Application.Interfaces;
using System.ClientModel;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace OboxSteam.Infrastructure.Services;

/// <summary>
/// Implements <see cref="IStrengthMatchService"/> using AWS Bedrock Mantle —
/// an OpenAI-compatible inference endpoint powered by Amazon Bedrock.
/// Uses the official OpenAI .NET SDK pointed at the regional Mantle endpoint.
/// Auth via Bedrock API Key (separate from AWS_ACCESS_KEY — generated in Bedrock console).
/// Endpoint: https://bedrock-mantle.{region}.api.aws/v1
/// </summary>
public class BedrockMantleStrengthMatchService : IStrengthMatchService
{
    // Note: The model ID (moonshotai.kimi-k2.5) is configured via DI in IocContainer.cs
    // and injected into this service via the ChatClient.

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly ChatClient _chatClient;
    private readonly ILogger<BedrockMantleStrengthMatchService> _logger;

    public BedrockMantleStrengthMatchService(
        ChatClient chatClient,
        ILogger<BedrockMantleStrengthMatchService> logger)
    {
        _chatClient = chatClient;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<StrengthMatchResult> MatchStrengthsAsync(
        IList<FaceTimestampSegment> faceSegments,
        IList<LabelDetectionEntry> labelTimeline,
        string strengthDescription,
        CancellationToken ct = default)
    {
        _logger.LogInformation(
            "BedrockMantleStrengthMatchService: {FaceSegs} face segment(s), {Labels} label(s), StrengthDescription: {Desc}",
            faceSegments.Count, labelTimeline.Count, strengthDescription);

        var prompt = BuildPrompt(faceSegments, labelTimeline, strengthDescription);

        ChatCompletion completion;
        try
        {
            completion = await _chatClient.CompleteChatAsync(
                [new UserChatMessage(prompt)],
                new ChatCompletionOptions
                {
                    Temperature = 0f,   // deterministic - consistent matching
                    MaxOutputTokenCount = 4096
                },
                ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "BedrockMantleStrengthMatchService: CompleteChatAsync failed");
            throw;
        }

        var rawText = completion.Content.FirstOrDefault()?.Text ?? string.Empty;

        _logger.LogInformation(
            "BedrockMantleStrengthMatchService: response length={Len}, finishReason={Reason}",
            rawText.Length, completion.FinishReason);

        if (string.IsNullOrWhiteSpace(rawText))
        {
            _logger.LogWarning("BedrockMantleStrengthMatchService: empty response. Returning empty match.");
            return new StrengthMatchResult(Array.Empty<MatchedSegment>(), "Mantle returned an empty response.");
        }

        // Claude may wrap the JSON in markdown fences - extract the JSON block.
        var rawJson = ExtractJson(rawText);

        ClaudeMatchOutput output;
        try
        {
            output = JsonSerializer.Deserialize<ClaudeMatchOutput>(rawJson, JsonOpts)
                     ?? throw new InvalidOperationException("Deserialized to null.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "BedrockMantleStrengthMatchService: failed to deserialize response. Raw={Raw}", rawJson);
            return new StrengthMatchResult(Array.Empty<MatchedSegment>(), "Failed to parse response.");
        }

        // Claude often copies the prompt example and returns end_ms=0 for point-in-time
        // detections. Treat missing/invalid end_ms as a single-sample segment [start, start].
        var matched = (output.MatchedSegments ?? Array.Empty<ClaudeSegment>())
            .Select(NormalizeClaudeSegment)
            .OrderByDescending(s => s.Score)
            .ToList();

        _logger.LogInformation(
            "BedrockMantleStrengthMatchService: {Matched} matched segment(s). Reasoning: {Reasoning}",
            matched.Count, output.Reasoning);

        return new StrengthMatchResult(matched, output.Reasoning ?? string.Empty);
    }

    // -- Private Helpers -------------------------------------------------------

    /// <summary>
    /// Maps a Claude JSON segment to <see cref="MatchedSegment"/>, coercing invalid
    /// <c>end_ms</c> values (0 or &lt; start_ms) to <c>start_ms</c> so point detections
    /// survive the clip merge step downstream.
    /// </summary>
    private static MatchedSegment NormalizeClaudeSegment(ClaudeSegment s)
    {
        var start = s.StartMs;
        var end = s.EndMs <= 0 || s.EndMs < start ? start : s.EndMs;
        return new MatchedSegment(start, end, s.Strength ?? string.Empty, s.Score);
    }

    /// <summary>
    /// Builds the user prompt. Identical sampling logic to BedrockStrengthMatchService -
    /// groups labels into 1-second buckets, samples up to 200 groups evenly.
    /// </summary>
    private static string BuildPrompt(
        IList<FaceTimestampSegment> faceSegments,
        IList<LabelDetectionEntry> labelTimeline,
        string strengthDescription)
    {
        var sb = new StringBuilder();

        sb.AppendLine("You are a video segment analyzer. Cross-reference the student face appearance segments with the label detection timeline to identify which segments show the student demonstrating their strengths.");
        sb.AppendLine();
        sb.AppendLine("STUDENT STRENGTH DESCRIPTION:");
        sb.AppendLine($"\"{strengthDescription}\"");
        sb.AppendLine();
        sb.AppendLine("STUDENT FACE APPEARANCE SEGMENTS (milliseconds):");

        foreach (var seg in faceSegments)
            sb.AppendLine($"  - {seg.StartMs}ms to {seg.EndMs}ms");

        sb.AppendLine();
        sb.AppendLine("LABEL DETECTION TIMELINE (timestamp_ms: label confidence):");

        // Group labels by 1-second bucket, sample up to 200 groups evenly across the video.
        const int MaxLabelGroups = 200;
        var allLabelGroups = labelTimeline
            .GroupBy(l => l.TimestampMs / 1000 * 1000)
            .OrderBy(g => g.Key)
            .ToList();

        var sampledGroups = allLabelGroups.Count <= MaxLabelGroups
            ? allLabelGroups
            : Enumerable.Range(0, MaxLabelGroups)
                .Select(i => allLabelGroups[(int)((double)i * allLabelGroups.Count / MaxLabelGroups)])
                .ToList();

        foreach (var grp in sampledGroups)
        {
            var labels = string.Join(", ", grp.Select(l => $"{l.LabelName} {l.Confidence:F0}pct"));
            sb.AppendLine($"  {grp.Key}ms: {labels}");
        }

        sb.AppendLine();
        sb.AppendLine("INSTRUCTIONS:");
        sb.AppendLine("1. For each face segment, check which labels appear during that time window.");
        sb.AppendLine("2. Semantically match labels to strengths (Soccer=football=da bong, Chess=danh co, Presentation=thuyet trinh).");
        sb.AppendLine("3. Assign a score 0.0-1.0: how well labels match the strength and how much of the segment has matching labels.");
        sb.AppendLine("4. Only include segments with score >= 0.5.");
        sb.AppendLine("5. Keep reasoning to ONE short sentence, max 20 words.");
        sb.AppendLine();
        sb.AppendLine("Respond with ONLY valid JSON, no extra text, no markdown fences:");
        sb.AppendLine("{");
        sb.AppendLine("  \"matched_segments\": [");
        sb.AppendLine("    { \"start_ms\": 6000, \"end_ms\": 6000, \"strength\": \"example\", \"score\": 0.9 }");
        sb.AppendLine("  ],");
        sb.AppendLine("  NOTE: end_ms MUST equal start_ms for a single detection instant; never use end_ms=0.");
        sb.AppendLine("  \"reasoning\": \"one short sentence\"");
        sb.AppendLine("}");

        return sb.ToString();
    }

    /// <summary>
    /// Extracts the JSON object from response text.
    /// Claude sometimes wraps output in triple-backtick markdown fences.
    /// </summary>
    private static string ExtractJson(string text)
    {
        var trimmed = text.Trim();

        // Strip markdown code fences if present
        if (trimmed.StartsWith("```", StringComparison.Ordinal))
        {
            var firstNewline = trimmed.IndexOf('\n');
            var lastFence = trimmed.LastIndexOf("```", StringComparison.Ordinal);

            if (firstNewline >= 0 && lastFence > firstNewline)
                return trimmed[(firstNewline + 1)..lastFence].Trim();
        }

        // Find the first and last braces to extract the JSON object
        var start = trimmed.IndexOf('{');
        var end = trimmed.LastIndexOf('}');
        if (start >= 0 && end > start)
            return trimmed[start..(end + 1)];

        return trimmed;
    }

    // -- Response DTOs (internal only) -----------------------------------------

    private sealed class ClaudeMatchOutput
    {
        [JsonPropertyName("matched_segments")]
        public ClaudeSegment[]? MatchedSegments { get; init; }

        [JsonPropertyName("reasoning")]
        public string? Reasoning { get; init; }
    }

    private sealed class ClaudeSegment
    {
        [JsonPropertyName("start_ms")]
        public long StartMs { get; init; }

        [JsonPropertyName("end_ms")]
        public long EndMs { get; init; }

        [JsonPropertyName("strength")]
        public string? Strength { get; init; }

        [JsonPropertyName("score")]
        public double Score { get; init; }
    }
}
