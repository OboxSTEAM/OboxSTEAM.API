using Amazon.BedrockRuntime;
using Amazon.BedrockRuntime.Model;
using Microsoft.Extensions.Logging;
using OboxSteam.Application.Interfaces;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace OboxSteam.Infrastructure.Services;

/// <summary>
/// Implements <see cref="IStrengthMatchService"/> using AWS Bedrock Converse API with Claude.
/// Uses Tool Use (structured output) so Claude is forced to return a well-typed JSON schema —
/// no manual JSON parsing or regex required.
/// </summary>
public class BedrockStrengthMatchService : IStrengthMatchService
{
    // Claude Haiku: fastest and cheapest model — sufficient for cross-reference reasoning.
    // Switch to claude-3-5-sonnet if accuracy needs improvement.
    private const string ModelId = "us.anthropic.claude-3-haiku-20240307-v1:0";

    // Bedrock Tool name — must match what Claude will call.
    private const string ToolName = "report_strength_matches";

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly IAmazonBedrockRuntime _bedrock;
    private readonly ILogger<BedrockStrengthMatchService> _logger;

    public BedrockStrengthMatchService(
        IAmazonBedrockRuntime bedrock,
        ILogger<BedrockStrengthMatchService> logger)
    {
        _bedrock = bedrock;
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
            "BedrockStrengthMatchService: {FaceSegs} face segments, {Labels} labels, StrengthDescription: {Desc}",
            faceSegments.Count, labelTimeline.Count, strengthDescription);

        var prompt = BuildPrompt(faceSegments, labelTimeline, strengthDescription);

        var request = new ConverseRequest
        {
            ModelId = ModelId,
            // Tool Use forces Claude to return the exact JSON schema — no prose parsing needed.
            ToolConfig = new ToolConfiguration
            {
                Tools = [BuildTool()],
                // REQUIRED: force Claude to call the tool (not return free text)
                ToolChoice = new ToolChoice { Tool = new SpecificToolChoice { Name = ToolName } }
            },
            Messages =
            [
                new Message
                {
                    Role = ConversationRole.User,
                    Content = [new ContentBlock { Text = prompt }]
                }
            ],
            InferenceConfig = new InferenceConfiguration
            {
                MaxTokens = 2048,
                Temperature = 0f  // deterministic — we want consistent matching
            }
        };

        ConverseResponse response;
        try
        {
            response = await _bedrock.ConverseAsync(request, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Bedrock ConverseAsync failed");
            throw;
        }

        // Extract the tool-use block from Claude's response
        var toolUseBlock = response.Output?.Message?.Content?
            .FirstOrDefault(c => c.ToolUse != null)?.ToolUse;

        if (toolUseBlock == null)
        {
            _logger.LogWarning("BedrockStrengthMatchService: no tool use block in response. Returning empty match.");
            return new StrengthMatchResult(
                Array.Empty<MatchedSegment>(),
                "Claude did not return a structured response.");
        }

        // Deserialize the structured JSON that Claude produced.
        // ToolUse.Input is an Amazon.Runtime.Documents.Document — serialize it back
        // to a JSON string first, then deserialize into our typed DTO.
        ClaudeMatchOutput output;
        try
        {
            // Convert the Document to a dictionary and re-serialize to standard JSON
            var inputDict = toolUseBlock.Input.AsDictionary();
            var json = System.Text.Json.JsonSerializer.Serialize(inputDict);
            output = System.Text.Json.JsonSerializer.Deserialize<ClaudeMatchOutput>(json, JsonOpts)
                     ?? throw new InvalidOperationException("Deserialized to null.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "BedrockStrengthMatchService: failed to deserialize tool output");
            return new StrengthMatchResult(
                Array.Empty<MatchedSegment>(),
                "Failed to parse Claude response.");
        }

        // Sort by score descending — highest quality segment first for MediaConvert ordering
        var matched = (output.MatchedSegments ?? Array.Empty<ClaudeSegment>())
            .Select(s => new MatchedSegment(s.StartMs, s.EndMs, s.Strength ?? string.Empty, s.Score))
            .OrderByDescending(s => s.Score)
            .ToList();

        _logger.LogInformation(
            "BedrockStrengthMatchService: {Matched} matched segment(s). Reasoning: {Reasoning}",
            matched.Count, output.Reasoning);

        return new StrengthMatchResult(matched, output.Reasoning ?? string.Empty);
    }

    // ── Private Helpers ───────────────────────────────────────────────────────

    /// <summary>
    /// Builds the user prompt with all context Claude needs to perform cross-reference.
    /// Kept intentionally concise — Haiku works well with dense, structured context.
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

        // Group labels by timestamp bucket (1s) for readability.
        // Sample evenly across the full timeline (max 200 groups) so labels
        // from the end of long videos are not silently dropped.
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
        sb.AppendLine("5. Call the report_strength_matches tool with your results.");

        return sb.ToString();
    }

    /// <summary>
    /// Defines the Bedrock Tool schema. Claude MUST call this tool — the ToolChoice
    /// configuration above enforces it, guaranteeing structured JSON output.
    /// </summary>
    private static Tool BuildTool() => new()
    {
        ToolSpec = new ToolSpecification
        {
            Name = ToolName,
            Description = "Report which face appearance segments match the student's strengths, with confidence scores.",
            InputSchema = new ToolInputSchema
            {
                Json = Amazon.Runtime.Documents.Document.FromObject(new
                {
                    type = "object",
                    properties = new
                    {
                        matched_segments = new
                        {
                            type = "array",
                            description = "List of segments that match at least one strength, sorted by score descending.",
                            items = new
                            {
                                type = "object",
                                properties = new
                                {
                                    start_ms = new { type = "integer", description = "Segment start in milliseconds" },
                                    end_ms = new { type = "integer", description = "Segment end in milliseconds" },
                                    strength = new { type = "string", description = "The matched strength label" },
                                    score = new { type = "number", description = "Match confidence 0.0-1.0" }
                                },
                                required = new[] { "start_ms", "end_ms", "strength", "score" }
                            }
                        },
                        reasoning = new
                        {
                            type = "string",
                            description = "Brief explanation of the matching decisions"
                        }
                    },
                    required = new[] { "matched_segments", "reasoning" }
                })
            }
        }
    };

    // ── Response DTOs (internal only) ─────────────────────────────────────────

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
