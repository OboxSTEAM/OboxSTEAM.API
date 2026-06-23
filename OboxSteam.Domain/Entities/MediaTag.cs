namespace OboxSteam.Domain.Entities;

/// <summary>
/// AI face detection results linking media to identified students.
/// Composite key: (MediaId, StudentId)
/// </summary>
public class MediaTag : BaseEntity
{
    public Guid MediaId { get; set; }
    public MediaAsset Media { get; set; } = null!;

    public Guid StudentId { get; set; }
    public User Student { get; set; } = null!;

    public decimal ConfidenceScore { get; set; }

    public bool IsVerified { get; set; } = true;

    /// <summary>
    /// JSON-serialized list of this student's face appearance segments in the video,
    /// captured at tagging time while the Rekognition job results were still fresh
    /// (Rekognition retains video job results for only 7 days). Format:
    /// <c>[{"StartMs":1000,"EndMs":4500}, ...]</c>. Null for image tags or legacy
    /// rows created before this field existed. The personal-video pipeline reads this
    /// instead of re-querying Rekognition, so highlight generation works indefinitely.
    /// </summary>
    public string? FaceSegmentsJson { get; set; }

    /// <summary>
    /// Whether the source video contained at least one face other than this student
    /// (another tagged student or an unrecognized person), captured at tagging time.
    /// Used by the personal-video pipeline to decide between full-video and
    /// segment-only clipping without re-querying Rekognition.
    /// </summary>
    public bool HasOtherFaces { get; set; }

    /// <summary>
    /// The anonymous AWS Transcribe speaker label (e.g. "spk_0") mapped to this student via
    /// overlap analysis between the student's face timeline and the speaker timeline.
    /// Null when no speaker could be mapped (no face/voice overlap, or no speaker data).
    /// </summary>
    public string? MappedSpeakerLabel { get; set; }

    /// <summary>
    /// JSON-serialized list of voice segments belonging to <see cref="MappedSpeakerLabel"/>,
    /// i.e. the time ranges where this student is speaking (including off-camera moments).
    /// Format: <c>[{"StartMs":1000,"EndMs":4500}, ...]</c>. The personal-video pipeline unions
    /// these with the face segments so highlights keep "voice but no face" moments.
    /// </summary>
    public string? VoiceSegmentsJson { get; set; }
}
