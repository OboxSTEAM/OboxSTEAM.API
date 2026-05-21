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

    [MaxLength(255)]
    /// <summary>
    /// Multi-purpose pipeline reference for video assets. Holds different values depending on <see cref="VideoStatus"/>:
    /// <list type="bullet">
    ///   <item><c>"raw:{s3key}"</c> — raw S3 key, waiting for MediaConvert submission</item>
    ///   <item><c>"mc:{jobId}"</c> — MediaConvert job ID, transcoding in progress</item>
    ///   <item><c>"{rekJobId}"</c> — Rekognition job ID, face-search in progress</item>
    /// </list>
    /// Always <c>null</c> for image assets.
    /// </summary>
    public string? VideoJobRef { get; set; }

    /// <summary>Tracks the background processing lifecycle for video assets.</summary>
    public VideoProcessingStatus VideoStatus { get; set; } = VideoProcessingStatus.None;

    public DateTime? UploadedAt { get; set; }

    // Navigation
    public ICollection<MediaTag> MediaTags { get; set; } = new List<MediaTag>();
    public ICollection<SubmissionEvidence> SubmissionEvidences { get; set; } = new List<SubmissionEvidence>();
}
