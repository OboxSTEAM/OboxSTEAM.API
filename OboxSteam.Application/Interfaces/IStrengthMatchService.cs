namespace OboxSteam.Application.Interfaces;

/// <summary>
/// Cross-references face appearance segments with label detection timeline
/// using Claude (via AWS Bedrock) to identify which segments show a student
/// demonstrating their specified strengths (e.g. football, chess, thuyết trình).
/// </summary>
public interface IStrengthMatchService
{
    /// <summary>
    /// Sends face timeline + label timeline + strengths list to Claude via Bedrock Converse API.
    /// Claude semantic-matches labels to strengths (handles Vietnamese, synonyms, etc.)
    /// and returns matched segments sorted by confidence score descending.
    /// </summary>
    /// <param name="faceSegments">Segments where the student's face was detected.</param>
    /// <param name="labelTimeline">Full label timeline from Rekognition Label Detection.</param>
    /// <param name="strengths">Student's strengths (e.g. ["football", "đánh cờ", "thuyết trình"]).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// <see cref="StrengthMatchResult"/> containing matched segments (sorted score desc)
    /// and Claude's reasoning text. <see cref="StrengthMatchResult.MatchedSegments"/> will be
    /// empty if no overlap was found — callers should treat this as a 400 scenario.
    /// </returns>
    Task<StrengthMatchResult> MatchStrengthsAsync(
        IList<FaceTimestampSegment> faceSegments,
        IList<LabelDetectionEntry> labelTimeline,
        string strengthDescription,
        CancellationToken ct = default);

    /// <summary>
    /// Scene-only path (no student face segments): scans the full label timeline and returns
    /// time ranges where visual labels demonstrate the described strength. Used for Case 1
    /// (tagged student, zero face detections) so clips are sub-ranges rather than the full video.
    /// </summary>
    Task<StrengthMatchResult> MatchStrengthsFromLabelsOnlyAsync(
        IList<LabelDetectionEntry> labelTimeline,
        string strengthDescription,
        CancellationToken ct = default);

    /// <summary>
    /// Off-camera speech path: the student is speaking during the given voice windows but their
    /// face is not visible. Cross-references each voice window with visual labels from the same
    /// (wider) time range to decide if the scene demonstrates the described strength.
    /// </summary>
    Task<StrengthMatchResult> MatchStrengthsForVoiceOnlyAsync(
        IList<FaceTimestampSegment> voiceOnlySegments,
        IList<LabelDetectionEntry> labelTimeline,
        string strengthDescription,
        CancellationToken ct = default);
}

/// <summary>
/// Result from <see cref="IStrengthMatchService.MatchStrengthsAsync"/>.
/// </summary>
/// <param name="MatchedSegments">
/// Segments where the student was detected AND a strength was identified,
/// ordered by <see cref="MatchedSegment.Score"/> descending (highest-quality first).
/// </param>
/// <param name="Reasoning">Claude's explanation of the matching decisions.</param>
public record StrengthMatchResult(
    IList<MatchedSegment> MatchedSegments,
    string Reasoning);

/// <summary>
/// A face segment that was semantically matched to one of the student's strengths.
/// </summary>
/// <param name="StartMs">Segment start in milliseconds.</param>
/// <param name="EndMs">Segment end in milliseconds.</param>
/// <param name="Strength">The strength label this segment was matched to.</param>
/// <param name="Score">Confidence score 0–1 assigned by Claude (higher = better match).</param>
public record MatchedSegment(long StartMs, long EndMs, string Strength, double Score);
