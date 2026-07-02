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
    private const double MinMatchScore = 0.55;

    /// <summary>Max 1-second label buckets sent to the LLM across all segment windows.</summary>
    private const int MaxLabelGroups = 400;

    /// <summary>Each face/voice window receives at least this many label buckets when downsampling.</summary>
    private const int MinGroupsPerSegment = 10;

    /// <summary>Windows at or below this span always include every label second-group (no cap).</summary>
    private const long SmallWindowMaxMs = 15_000;

    private static readonly HashSet<string> GenericLabelNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "Person", "People", "Human", "Hand", "Hands", "Finger", "Arm", "Head", "Face",
        "Clothing", "Apparel", "Portrait", "Photography", "Microphone", "Indoors", "Room",
        "Furniture", "Table", "Chair", "Wall", "Floor", "Ceiling", "Light", "Lighting"
    };

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
        _logger.LogInformation(
            "BedrockMantleStrengthMatchService: sending prompt ({Chars} chars, ~{EstTokens} est. tokens)",
            prompt.Length, prompt.Length / 4);

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
                "BedrockMantleStrengthMatchService: raw match [{Start}ms→{End}ms] span={Span}ms score={Score:0.00} strength={Strength} evidence=[{Evidence}] {Kept}",
                s.StartMs, s.EndMs, s.EndMs - s.StartMs, s.Score, s.Strength,
                string.Join(", ", s.EvidenceLabels),
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
        var evidence = (s.EvidenceLabels ?? Array.Empty<string>())
            .Where(l => !string.IsNullOrWhiteSpace(l))
            .ToList();

        return new MatchedSegment(start, end, s.Strength ?? string.Empty, s.Score, evidence);
    }

    private static void AppendMatchResponseSchema(StringBuilder sb, string exampleEndMs)
    {
        sb.AppendLine("Respond with ONLY valid JSON, no extra text, no markdown fences:");
        sb.AppendLine("{");
        sb.AppendLine("  \"matched_segments\": [");
        sb.AppendLine(
            $"    {{ \"start_ms\": 6000, \"end_ms\": {exampleEndMs}, \"strength\": \"example\", \"score\": 0.9, \"evidence_labels\": [\"Eating\", \"Food\"] }}");
        sb.AppendLine("  ],");
        sb.AppendLine("  \"reasoning\": \"one short sentence\"");
        sb.AppendLine("}");
    }

    private static void AppendEvidenceLabelInstructions(StringBuilder sb, int startIndex)
    {
        sb.AppendLine(
            $"{startIndex}. evidence_labels: string array of label names from the LABEL DETECTION TIMELINE when available (prefer activity labels over generic Person/Hand).");
        sb.AppendLine(
            $"{startIndex + 1}. start_ms/end_ms should cover the matched activity; evidence_labels help justify the range when present.");
    }

    private string BuildPrompt(
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
        AppendSegmentScopedLabelTimeline(sb, faceSegments, labelTimeline);

        sb.AppendLine();
        sb.AppendLine("INSTRUCTIONS:");
        sb.AppendLine("1. Evaluate each WINDOW independently using ONLY the labels listed under that WINDOW. Do not infer from labels outside the window.");
        sb.AppendLine("2. Match a label to the strength when the label reasonably represents that strength (Soccer=football=da bong, Chess=danh co, Presentation=thuyet trinh, Karate/Boxing/Martial Arts=vo thuat). Prefer recall — include segments with plausible visual evidence.");
        sb.AppendLine("3. A single specific activity label can be enough. Generic labels alone (Person, Hand, Clothing) are weak evidence — pair with an activity label when possible.");
        sb.AppendLine("4. Assign a score 0.0-1.0 reflecting how well the labels support the strength.");
        sb.AppendLine($"5. Include segments with score >= {MinMatchScore:0.00}. When evidence is partial but relevant, include rather than exclude.");
        sb.AppendLine("6. If NO segment plausibly demonstrates the strength, return an empty matched_segments array.");
        sb.AppendLine("7. Keep reasoning to ONE short sentence, max 20 words.");
        AppendEvidenceLabelInstructions(sb, 8);
        sb.AppendLine();
        AppendMatchResponseSchema(sb, "6000");

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
        AppendActivityPrioritizedLabelTimeline(sb, labelTimeline);
        sb.AppendLine();
        sb.AppendLine("INSTRUCTIONS:");
        sb.AppendLine("1. Find contiguous or nearby label timestamps where labels reasonably represent the strength (Soccer=football=da bong, Chess=danh co, Presentation=thuyet trinh, Karate/Boxing/Martial Arts=vo thuat).");
        sb.AppendLine("2. Return matched_segments with start_ms and end_ms taken from the label timestamps (merge nearby matches into one range).");
        sb.AppendLine("3. A single specific activity label can be enough. Prefer recall over precision.");
        sb.AppendLine("4. Assign score 0.0-1.0 per segment.");
        sb.AppendLine($"5. Include segments with score >= {MinMatchScore:0.00}.");
        sb.AppendLine("6. If NO time range plausibly demonstrates the strength, return an empty matched_segments array.");
        sb.AppendLine("7. Keep reasoning to ONE short sentence, max 20 words.");
        AppendEvidenceLabelInstructions(sb, 8);
        sb.AppendLine();
        AppendMatchResponseSchema(sb, "12000");

        return sb.ToString();
    }

    private string BuildVoiceOnlyPrompt(
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
        AppendSegmentScopedLabelTimeline(sb, voiceOnlySegments, labelTimeline);

        sb.AppendLine();
        sb.AppendLine("INSTRUCTIONS:");
        sb.AppendLine("1. Evaluate each WINDOW independently using ONLY the labels listed under that WINDOW.");
        sb.AppendLine("2. Match when labels reasonably represent the strength (Soccer=football=da bong, Chess=danh co, Presentation=thuyet trinh, Karate/Boxing/Martial Arts=vo thuat). Prefer recall.");
        sb.AppendLine("3. A single specific activity label can be enough.");
        sb.AppendLine("4. Return matched_segments using timestamps where labels demonstrate the strength — prefer a focused range, not necessarily the entire voice window.");
        sb.AppendLine("5. Assign score 0.0-1.0 per segment.");
        sb.AppendLine($"6. Include segments with score >= {MinMatchScore:0.00}.");
        sb.AppendLine("7. If NO voice window has supporting visual labels, return an empty matched_segments array.");
        sb.AppendLine("8. Keep reasoning to ONE short sentence, max 20 words.");
        AppendEvidenceLabelInstructions(sb, 9);
        sb.AppendLine();
        AppendMatchResponseSchema(sb, "12000");

        return sb.ToString();
    }

    /// <summary>
    /// Lists labels under each face/voice window so the LLM evaluates segments with local
    /// context instead of a flat uniformly-sampled timeline.
    /// </summary>
    private void AppendSegmentScopedLabelTimeline(
        StringBuilder sb,
        IList<FaceTimestampSegment> segments,
        IList<LabelDetectionEntry> labelTimeline)
    {
        sb.AppendLine("LABEL DETECTION TIMELINE BY WINDOW (labels listed under each window only):");

        var perSegment = segments
            .Select(seg =>
            {
                var groups = labelTimeline
                    .Where(l => l.TimestampMs >= seg.StartMs && l.TimestampMs <= seg.EndMs)
                    .GroupBy(l => l.TimestampMs / 1000 * 1000)
                    .OrderBy(g => g.Key)
                    .ToList();
                return (Seg: seg, Groups: groups);
            })
            .ToList();

        var selected = AllocateSegmentLabelGroups(perSegment);

        foreach (var (seg, groups) in selected)
        {
            sb.AppendLine($"WINDOW {seg.StartMs}ms → {seg.EndMs}ms:");
            if (groups.Count == 0)
            {
                sb.AppendLine("  (no labels in this window)");
                sb.AppendLine();
                continue;
            }

            foreach (var grp in groups)
            {
                var labels = string.Join(", ", grp.Select(l => $"{l.LabelName} {l.Confidence:F0}pct"));
                sb.AppendLine($"  {grp.Key}ms: {labels}");
            }

            sb.AppendLine();
        }
    }

    /// <summary>Scene-only path: activity-priority sampling across the full timeline.</summary>
    private static void AppendActivityPrioritizedLabelTimeline(
        StringBuilder sb,
        IList<LabelDetectionEntry> labelTimeline)
    {
        sb.AppendLine("LABEL DETECTION TIMELINE (timestamp_ms: label confidence):");

        var allGroups = labelTimeline
            .GroupBy(l => l.TimestampMs / 1000 * 1000)
            .OrderBy(g => g.Key)
            .ToList();

        var sampled = allGroups.Count <= MaxLabelGroups
            ? allGroups
            : RankSecondGroups(allGroups, null)
                .Take(MaxLabelGroups)
                .OrderBy(g => g.Key)
                .ToList();

        foreach (var grp in sampled)
        {
            var labels = string.Join(", ", grp.Select(l => $"{l.LabelName} {l.Confidence:F0}pct"));
            sb.AppendLine($"  {grp.Key}ms: {labels}");
        }
    }

    private List<(FaceTimestampSegment Seg, List<IGrouping<long, LabelDetectionEntry>> Groups)>
        AllocateSegmentLabelGroups(
            List<(FaceTimestampSegment Seg, List<IGrouping<long, LabelDetectionEntry>> Groups)> perSegment)
    {
        var totalAll = perSegment.Sum(p => p.Groups.Count);
        if (totalAll <= MaxLabelGroups)
        {
            _logger.LogDebug(
                "AllocateSegmentLabelGroups: {Total} total groups within budget ({Budget}), no downsampling needed",
                totalAll, MaxLabelGroups);
            return perSegment;
        }

        _logger.LogInformation(
            "AllocateSegmentLabelGroups: downsampling {Total} total groups to budget={Budget} across {Segments} segment(s)",
            totalAll, MaxLabelGroups, perSegment.Count);

        var rankedPerSegment = perSegment
            .Select(p => (p.Seg, Ranked: RankSecondGroups(p.Groups, p.Seg), p.Groups.Count))
            .ToList();

        var selected = Enumerable.Range(0, perSegment.Count)
            .Select(_ => new List<IGrouping<long, LabelDetectionEntry>>())
            .ToList();
        var remaining = MaxLabelGroups;

        // Pass 1: small windows and mandatory minimums
        for (var i = 0; i < rankedPerSegment.Count; i++)
        {
            var (seg, ranked, count) = rankedPerSegment[i];
            if (count == 0)
                continue;

            var span = seg.EndMs - seg.StartMs;
            int take;
            if (span <= SmallWindowMaxMs)
                take = count;
            else
                take = Math.Min(MinGroupsPerSegment, count);

            take = Math.Min(take, remaining);
            selected[i] = ranked.Take(take).ToList();
            remaining -= selected[i].Count;
        }

        // Pass 2: distribute leftover budget round-robin by priority
        if (remaining > 0)
        {
            var cursors = rankedPerSegment
                .Select((p, i) => (Index: i, Cursor: selected[i].Count, p.Ranked))
                .Where(x => x.Cursor < x.Ranked.Count)
                .ToList();

            while (remaining > 0 && cursors.Count > 0)
            {
                var nextCursors = new List<(int Index, int Cursor, List<IGrouping<long, LabelDetectionEntry>> Ranked)>();
                foreach (var (index, cursor, ranked) in cursors)
                {
                    if (remaining == 0)
                        break;

                    selected[index].Add(ranked[cursor]);
                    remaining--;

                    var next = cursor + 1;
                    if (next < ranked.Count)
                        nextCursors.Add((index, next, ranked));
                }

                cursors = nextCursors;
            }
        }

        return perSegment
            .Select((p, i) => (p.Seg, Groups: selected[i].OrderBy(g => g.Key).ToList()))
            .ToList();
    }

    private static List<IGrouping<long, LabelDetectionEntry>> RankSecondGroups(
        IList<IGrouping<long, LabelDetectionEntry>> groups,
        FaceTimestampSegment? window)
    {
        var startSecond = window != null ? window.StartMs / 1000 * 1000 : (long?)null;
        var endSecond = window != null ? window.EndMs / 1000 * 1000 : (long?)null;

        return groups
            .OrderByDescending(g => SecondGroupPriority(g, startSecond, endSecond))
            .ThenBy(g => g.Key)
            .ToList();
    }

    private static int SecondGroupPriority(
        IGrouping<long, LabelDetectionEntry> group,
        long? windowStartSecond,
        long? windowEndSecond)
    {
        var isBoundary = windowStartSecond.HasValue && windowEndSecond.HasValue
                         && (group.Key == windowStartSecond.Value || group.Key == windowEndSecond.Value);
        var activityMax = group
            .Where(l => !IsGenericLabel(l.LabelName))
            .Select(l => l.Confidence)
            .DefaultIfEmpty(0)
            .Max();

        var hasActivity = activityMax >= 60;
        return (isBoundary ? 10_000 : 0) + (hasActivity ? 5_000 : 0) + (int)activityMax;
    }

    private static bool IsGenericLabel(string labelName) => GenericLabelNames.Contains(labelName);

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

        [JsonPropertyName("evidence_labels")]
        public string[]? EvidenceLabels { get; init; }
    }
}
