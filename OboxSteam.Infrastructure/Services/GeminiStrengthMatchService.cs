using Google.GenAI;
using Google.GenAI.Types;
using Microsoft.Extensions.Logging;
using OboxSteam.Application.Interfaces;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using GeminiType = Google.GenAI.Types.Type;

namespace OboxSteam.Infrastructure.Services;

/// <summary>
/// Implements <see cref="IStrengthMatchService"/> using the Google Gemini API
/// with structured JSON output (ResponseMimeType = application/json + ResponseSchema).
/// </summary>
public class GeminiStrengthMatchService : IStrengthMatchService
{

    private const string ModelId = "gemini-2.5-flash";

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly Client _client;
    private readonly ILogger<GeminiStrengthMatchService> _logger;

    public GeminiStrengthMatchService(
        Client client,
        ILogger<GeminiStrengthMatchService> logger)
    {
        _client = client;
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
            "GeminiStrengthMatchService: {FaceSegs} face segments, {Labels} labels, StrengthDescription: {Desc}",
            faceSegments.Count, labelTimeline.Count, strengthDescription);

        var prompt = BuildPrompt(faceSegments, labelTimeline, strengthDescription);

        // Structured output: force Gemini to return a typed JSON object matching our schema.
        var config = new GenerateContentConfig
        {
            ResponseMimeType = "application/json",
            ResponseSchema = new Schema
            {
                Type = GeminiType.Object,
                Properties = new Dictionary<string, Schema>
                {
                    ["matched_segments"] = new Schema
                    {
                        Type = GeminiType.Array,
                        Description = "List of segments that match the student's strength, sorted by score descending.",
                        Items = new Schema
                        {
                            Type = GeminiType.Object,
                            Properties = new Dictionary<string, Schema>
                            {
                                ["start_ms"] = new Schema { Type = GeminiType.Integer, Description = "Segment start in milliseconds" },
                                ["end_ms"] = new Schema { Type = GeminiType.Integer, Description = "Segment end in milliseconds" },
                                ["strength"] = new Schema { Type = GeminiType.String, Description = "The matched strength label" },
                                ["score"] = new Schema { Type = GeminiType.Number, Description = "Match confidence 0.0-1.0" }
                            },
                            Required = ["start_ms", "end_ms", "strength", "score"]
                        }
                    },
                    ["reasoning"] = new Schema
                    {
                        Type = GeminiType.String,
                        Description = "Brief explanation of the matching decisions"
                    }
                },
                Required = ["matched_segments", "reasoning"]
            },
            Temperature = 0f,       // deterministic — consistent matching
            MaxOutputTokens = 8192, // safety net against long responses
            // gemini-2.5-flash enables thinking by default — thinking tokens also count
            // against MaxOutputTokens, leaving too few tokens for the actual JSON output.
            // This is a simple label-matching task; thinking is unnecessary overhead.
            ThinkingConfig = new ThinkingConfig { ThinkingBudget = 0 }
        };

        GenerateContentResponse response;
        try
        {
            response = await _client.Models.GenerateContentAsync(
                model: ModelId,
                contents: prompt,
                config: config);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GeminiStrengthMatchService: GenerateContentAsync failed");
            throw;
        }

        var rawJson = response.Text;
        if (string.IsNullOrWhiteSpace(rawJson))
        {
            _logger.LogWarning("GeminiStrengthMatchService: empty response text. Returning empty match.");
            return new StrengthMatchResult(Array.Empty<MatchedSegment>(), "Gemini returned an empty response.");
        }

        GeminiMatchOutput output;
        try
        {
            output = JsonSerializer.Deserialize<GeminiMatchOutput>(rawJson, JsonOpts)
                     ?? throw new InvalidOperationException("Deserialized to null.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GeminiStrengthMatchService: failed to deserialize response JSON. Raw={Raw}", rawJson);
            return new StrengthMatchResult(Array.Empty<MatchedSegment>(), "Failed to parse Gemini response.");
        }

        var matched = (output.MatchedSegments ?? Array.Empty<GeminiSegment>())
            .Select(s => new MatchedSegment(s.StartMs, s.EndMs, s.Strength ?? string.Empty, s.Score))
            .OrderByDescending(s => s.Score)
            .ToList();

        _logger.LogInformation(
            "GeminiStrengthMatchService: {Matched} matched segment(s). Reasoning: {Reasoning}",
            matched.Count, output.Reasoning);

        return new StrengthMatchResult(matched, output.Reasoning ?? string.Empty);
    }

    // ── Private Helpers ───────────────────────────────────────────────────────

    /// <summary>
    /// Builds the user prompt — identical logic to the previous BedrockStrengthMatchService.
    /// Gemini handles the JSON schema constraint via GenerateContentConfig.ResponseSchema,
    /// so the prompt does NOT need to repeat the schema structure.
    /// </summary>
    private static string BuildPrompt(
        IList<FaceTimestampSegment> faceSegments,
        IList<LabelDetectionEntry> labelTimeline,
        string strengthDescription)
    {
        var sb = new StringBuilder();

        sb.AppendLine("You are a video segment analyzer. Cross-reference the student's face appearance segments with the label detection timeline to identify which segments show the student demonstrating their strengths.");
        sb.AppendLine();
        sb.AppendLine("STUDENT STRENGTH DESCRIPTION:");
        sb.AppendLine($"\"{strengthDescription}\"");
        sb.AppendLine();
        sb.AppendLine("STUDENT FACE APPEARANCE SEGMENTS (milliseconds):");

        foreach (var seg in faceSegments)
            sb.AppendLine($"  - {seg.StartMs}ms → {seg.EndMs}ms");

        sb.AppendLine();
        sb.AppendLine("LABEL DETECTION TIMELINE (timestamp_ms: label [confidence%]):");

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
            var labels = string.Join(", ", grp.Select(l => $"{l.LabelName}({l.Confidence:F0}%)"));
            sb.AppendLine($"  {grp.Key}ms: {labels}");
        }

        sb.AppendLine();
        sb.AppendLine("INSTRUCTIONS:");
        sb.AppendLine("1. For each face segment, check which labels appear during that time window.");
        sb.AppendLine("2. Semantically match labels to strengths (e.g. 'Soccer'='football'='đá bóng', 'Chess'='đánh cờ', 'Presentation'='thuyết trình').");
        sb.AppendLine("3. Assign a score 0.0–1.0 based on how well the labels match the strength and how much of the segment has matching labels.");
        sb.AppendLine("4. Only include segments with score >= 0.5.");
        sb.AppendLine("5. Keep 'reasoning' to ONE short sentence (max 20 words). Do not write long explanations.");

        return sb.ToString();
    }

    // ── Response DTOs (internal only) ─────────────────────────────────────────

    private sealed class GeminiMatchOutput
    {
        [JsonPropertyName("matched_segments")]
        public GeminiSegment[]? MatchedSegments { get; init; }

        [JsonPropertyName("reasoning")]
        public string? Reasoning { get; init; }
    }

    private sealed class GeminiSegment
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
