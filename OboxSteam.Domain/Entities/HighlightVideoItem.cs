using System.ComponentModel.DataAnnotations;
using OboxSteam.Domain.Enums;

namespace OboxSteam.Domain.Entities;

/// <summary>
/// One output video within a <see cref="HighlightVideoStack"/> (initial generation or trim).
/// </summary>
public class HighlightVideoItem : BaseEntity
{
    public Guid StackId { get; set; }
    public HighlightVideoStack Stack { get; set; } = null!;

    /// <summary>Parent item when this row is a trim of an existing output.</summary>
    public Guid? ParentItemId { get; set; }
    public HighlightVideoItem? ParentItem { get; set; }

    public HighlightVideoGenerationKind GenerationKind { get; set; }

    public string? VideoUrl { get; set; }

    [MaxLength(1024)]
    public string? OutputS3Key { get; set; }

    public long? DurationMs { get; set; }

    [MaxLength(255)]
    public string? PersonalVideoJobRef { get; set; }

    public HighlightVideoStatus Status { get; set; } = HighlightVideoStatus.Processing;

    public DateTime? RequestedAt { get; set; }

    [MaxLength(1024)]
    public string? FailureReason { get; set; }

    [MaxLength(2000)]
    public string? TrimDescription { get; set; }

    /// <summary>JSON array of exclude ranges on the parent output timeline (trim jobs only).</summary>
    public string? TrimExcludeRangesJson { get; set; }
}
