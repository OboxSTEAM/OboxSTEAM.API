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

    /// <summary>
    /// Minimum score for a segment to count as a strength match. Enforced both in the prompt
    /// and again in code as a safety net against speculative low-confidence matches.
    /// Raise toward 1.0 for stricter (fewer false positives) matching.
    /// </summary>
    private const double MinMatchScore = 0.6;

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
        return await CompleteMatchAsync(prompt, ct);
    }

    /// <inheritdoc />
    public async Task<StrengthMatchResult> MatchStrengthsFromLabelsOnlyAsync(
        IList<LabelDetectionEntry> labelTimeline,
        string strengthDescription,
        CancellationToken ct = default)
    {
        _logger.LogInformation(
            "BedrockMantleStrengthMatchService (label-only): {Labels} label(s), StrengthDescription: {Desc}",
            labelTimeline.Count, strengthDescription);

        var prompt = BuildLabelOnlyPrompt(labelTimeline, strengthDescription);
        return await CompleteMatchAsync(prompt, ct);
    }

    /// <inheritdoc />
    public async Task<StrengthMatchResult> MatchStrengthsForVoiceOnlyAsync(
        IList<FaceTimestampSegment> voiceOnlySegments,
        IList<LabelDetectionEntry> labelTimeline,
        string strengthDescription,
        CancellationToken ct = default)
    {
        _logger.LogInformation(
            "BedrockMantleStrengthMatchService (voice-only): {VoiceSegs} voice segment(s), {Labels} label(s), StrengthDescription: {Desc}",
            voiceOnlySegments.Count, labelTimeline.Count, strengthDescription);

        var prompt = BuildVoiceOnlyPrompt(voiceOnlySegments, labelTimeline, strengthDescription);
        return await CompleteMatchAsync(prompt, ct);
    }

    // -- Private Helpers -------------------------------------------------------

    private async Task<StrengthMatchResult> CompleteMatchAsync(string prompt, CancellationToken ct)
    {
        ChatCompletion completion;
        try
        {
            completion = await _chatClient.CompleteChatAsync(
                [new UserChatMessage(prompt)],
                new ChatCompletionOptions
                {
                    Temperature = 0f,
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

        var normalized = (output.MatchedSegments ?? Array.Empty<ClaudeSegment>())
            .Select(NormalizeClaudeSegment)
            .OrderByDescending(s => s.Score)
            .ToList();

        foreach (var s in normalized)
            _logger.LogInformation(
                "BedrockMantleStrengthMatchService: raw match [{Start}ms→{End}ms] span={Span}ms score={Score:0.00} strength={Strength} {Kept}",
                s.StartMs, s.EndMs, s.EndMs - s.StartMs, s.Score, s.Strength,
                s.Score >= MinMatchScore ? "KEPT" : "DROPPED(<threshold)");

        var matched = normalized
            .Where(s => s.Score >= MinMatchScore)
            .ToList();

        _logger.LogInformation(
            "BedrockMantleStrengthMatchService: {Matched} matched segment(s) (>= {Threshold}). Reasoning: {Reasoning}",
            matched.Count, MinMatchScore, output.Reasoning);

        return new StrengthMatchResult(matched, output.Reasoning ?? string.Empty);
    }

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
        AppendSampledLabelTimeline(sb, labelTimeline);

        sb.AppendLine();
        sb.AppendLine("INSTRUCTIONS:");
        sb.AppendLine("1. For each face segment, check which labels appear during that time window.");
        sb.AppendLine("2. Match a label to the strength ONLY when the label DIRECTLY and UNAMBIGUOUSLY represents that strength (Soccer=football=da bong, Chess=danh co, Presentation=thuyet trinh, Karate/Boxing/Martial Arts=vo thuat).");
        sb.AppendLine("3. DO NOT match on weak, generic, or speculative evidence. A single ambiguous label (e.g. 'Slapping', 'Hand', 'Person', 'People', 'Clothing') is NOT enough. If you find yourself reasoning that a label 'suggests', 'could be', 'might be', or 'is related to' the strength, treat it as NO match.");
        sb.AppendLine("4. Require multiple corroborating labels OR one strong, specific label that names the activity itself before matching.");
        sb.AppendLine("5. Assign a score 0.0-1.0 reflecting how directly and consistently the labels demonstrate the strength across the segment.");
        sb.AppendLine($"6. Only include segments with score >= {MinMatchScore:0.0}. When in doubt, exclude the segment.");
        sb.AppendLine("7. If NO segment clearly demonstrates the strength, return an empty matched_segments array.");
        sb.AppendLine("8. Keep reasoning to ONE short sentence, max 20 words.");
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

    private static string BuildLabelOnlyPrompt(
        IList<LabelDetectionEntry> labelTimeline,
        string strengthDescription)
    {
        var sb = new StringBuilder();

        sb.AppendLine("You are a video segment analyzer. The student's face was NOT detected in this video (scene-only / activity footage), but the video was tagged for this student.");
        sb.AppendLine("Scan the full label detection timeline and identify time ranges where the VISUAL content demonstrates the student's described strength.");
        sb.AppendLine();
        sb.AppendLine("STUDENT STRENGTH DESCRIPTION:");
        sb.AppendLine($"\"{strengthDescription}\"");
        sb.AppendLine();
        AppendSampledLabelTimeline(sb, labelTimeline);
        sb.AppendLine();
        sb.AppendLine("INSTRUCTIONS:");
        sb.AppendLine("1. Find contiguous or nearby label timestamps where labels DIRECTLY and UNAMBIGUOUSLY represent the strength (Soccer=football=da bong, Chess=danh co, Presentation=thuyet trinh, Karate/Boxing/Martial Arts=vo thuat).");
        sb.AppendLine("2. Return matched_segments with start_ms and end_ms taken from the label timestamps (merge nearby matches into one range).");
        sb.AppendLine("3. DO NOT match on weak, generic, or speculative evidence. A single ambiguous label (e.g. 'Person', 'Hand') is NOT enough.");
        sb.AppendLine("4. Require multiple corroborating labels OR one strong, specific label that names the activity itself.");
        sb.AppendLine("5. Assign score 0.0-1.0 per segment.");
        sb.AppendLine($"6. Only include segments with score >= {MinMatchScore:0.0}. When in doubt, exclude.");
        sb.AppendLine("7. If NO time range clearly demonstrates the strength, return an empty matched_segments array.");
        sb.AppendLine("8. Keep reasoning to ONE short sentence, max 20 words.");
        sb.AppendLine();
        sb.AppendLine("Respond with ONLY valid JSON, no extra text, no markdown fences:");
        sb.AppendLine("{");
        sb.AppendLine("  \"matched_segments\": [");
        sb.AppendLine("    { \"start_ms\": 6000, \"end_ms\": 12000, \"strength\": \"example\", \"score\": 0.9 }");
        sb.AppendLine("  ],");
        sb.AppendLine("  \"reasoning\": \"one short sentence\"");
        sb.AppendLine("}");

        return sb.ToString();
    }

    private static string BuildVoiceOnlyPrompt(
        IList<FaceTimestampSegment> voiceOnlySegments,
        IList<LabelDetectionEntry> labelTimeline,
        string strengthDescription)
    {
        var sb = new StringBuilder();

        sb.AppendLine("You are a video segment analyzer. The mapped student is SPEAKING OFF-CAMERA during the voice windows below (their face is not visible, but diarization confirms it is their voice).");
        sb.AppendLine("For each voice window, examine the VISUAL labels from the label detection timeline in the same time range and decide whether the on-screen scene demonstrates the student's described strength.");
        sb.AppendLine("You are NOT evaluating what the student says — only whether the visuals during their speech match the strength.");
        sb.AppendLine();
        sb.AppendLine("STUDENT STRENGTH DESCRIPTION:");
        sb.AppendLine($"\"{strengthDescription}\"");
        sb.AppendLine();
        sb.AppendLine("OFF-CAMERA VOICE WINDOWS (milliseconds):");

        foreach (var seg in voiceOnlySegments)
            sb.AppendLine($"  - {seg.StartMs}ms to {seg.EndMs}ms");

        sb.AppendLine();
        AppendSampledLabelTimeline(sb, labelTimeline);
        sb.AppendLine();
        sb.AppendLine("INSTRUCTIONS:");
        sb.AppendLine("1. For each voice window, check which visual labels appear during that same time range.");
        sb.AppendLine("2. Match ONLY when labels DIRECTLY and UNAMBIGUOUSLY represent the strength (Soccer=football=da bong, Chess=danh co, Presentation=thuyet trinh, Karate/Boxing/Martial Arts=vo thuat).");
        sb.AppendLine("3. DO NOT match on weak, generic, or speculative evidence. A single ambiguous label (e.g. 'Person', 'Hand', 'Microphone') is NOT enough.");
        sb.AppendLine("4. Require multiple corroborating labels OR one strong, specific label that names the activity itself.");
        sb.AppendLine("5. Return matched_segments using the VOICE window timestamps (start_ms/end_ms from the voice windows above) when labels in that window demonstrate the strength.");
        sb.AppendLine("6. Assign score 0.0-1.0 per segment.");
        sb.AppendLine($"7. Only include segments with score >= {MinMatchScore:0.0}. When in doubt, exclude.");
        sb.AppendLine("8. If NO voice window has supporting visual labels, return an empty matched_segments array.");
        sb.AppendLine("9. Keep reasoning to ONE short sentence, max 20 words.");
        sb.AppendLine();
        sb.AppendLine("Respond with ONLY valid JSON, no extra text, no markdown fences:");
        sb.AppendLine("{");
        sb.AppendLine("  \"matched_segments\": [");
        sb.AppendLine("    { \"start_ms\": 6000, \"end_ms\": 12000, \"strength\": \"example\", \"score\": 0.9 }");
        sb.AppendLine("  ],");
        sb.AppendLine("  \"reasoning\": \"one short sentence\"");
        sb.AppendLine("}");

        return sb.ToString();
    }

    private static void AppendSampledLabelTimeline(StringBuilder sb, IList<LabelDetectionEntry> labelTimeline)
    {
        sb.AppendLine("LABEL DETECTION TIMELINE (timestamp_ms: label confidence):");

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
