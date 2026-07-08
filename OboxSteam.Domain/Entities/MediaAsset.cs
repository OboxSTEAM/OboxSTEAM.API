using System.ComponentModel.DataAnnotations;
using OboxSteam.Domain.Enums;

namespace OboxSteam.Domain.Entities;

public class MediaAsset : BaseEntity
{
    public Guid UploaderId { get; set; }
    public User Uploader { get; set; } = null!;

    public Guid? ActivityId { get; set; }
    public Activity? Activity { get; set; }

    public string? FileUrl { get; set; }

    [MaxLength(50)]
    public string? FileType { get; set; }

    [MaxLength(512)]
    public string? RawVideoS3Key { get; set; }

    [MaxLength(512)]
    public string? MediaConvertJobId { get; set; }

    [MaxLength(512)]
    public string? FaceSearchJobId { get; set; }

    /// <summary>Tracks the background processing lifecycle for video assets.</summary>
    public VideoProcessingStatus VideoStatus { get; set; } = VideoProcessingStatus.None;

    /// <summary>
    /// Rekognition Label Detection job ID, populated alongside <see cref="FaceSearchJobId"/>
    /// once transcoding completes. Null until label detection has been triggered.
    /// Used by the strengths-based highlight filtering pipeline to retrieve a
    /// per-frame label timeline (Soccer, Chess, Presentation, …).
    /// </summary>
    [MaxLength(512)]
    public string? LabelJobRef { get; set; }

    /// <summary>
    /// JSON-serialized Rekognition Label Detection timeline, captured when the label
    /// detection webhook reports SUCCEEDED (results are retained by Rekognition for only
    /// 7 days). Format: <c>[{"TimestampMs":2000,"LabelName":"Soccer","Confidence":88.3}, ...]</c>.
    /// Null until the label job completes, or if capture failed. The strengths-filtering
    /// pipeline reads this instead of re-querying Rekognition, so highlight generation
    /// works indefinitely regardless of the 7-day window.
    /// </summary>
    public string? LabelTimelineJson { get; set; }

    public DateTime? UploadedAt { get; set; }

    // Navigation
    public ICollection<MediaTag> MediaTags { get; set; } = new List<MediaTag>();
    public ICollection<SubmissionEvidence> SubmissionEvidences { get; set; } = new List<SubmissionEvidence>();
}
