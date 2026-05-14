using System.ComponentModel.DataAnnotations;

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
    public string? RekognitionJobId { get; set; }

    public DateTime? UploadedAt { get; set; }

    // Navigation
    public ICollection<MediaTag> MediaTags { get; set; } = new List<MediaTag>();
    public ICollection<SubmissionEvidence> SubmissionEvidences { get; set; } = new List<SubmissionEvidence>();
}
