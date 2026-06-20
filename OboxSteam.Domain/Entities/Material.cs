using System.ComponentModel.DataAnnotations;
using OboxSteam.Domain.Enums;

namespace OboxSteam.Domain.Entities;

/// <summary>
/// Learning asset for a SelfPaced activity (video, PDF, doc, etc.). At most one per activity.
/// </summary>
public class Material : BaseEntity
{
    public Guid ActivityId { get; set; }
    public Activity Activity { get; set; } = null!;

    [MaxLength(255)]
    public string Title { get; set; } = null!;

    public MaterialType MaterialType { get; set; }

    public string? FileUrl { get; set; }

    /// <summary>Original file size in bytes.</summary>
    public long? FileSizeBytes { get; set; }
}
