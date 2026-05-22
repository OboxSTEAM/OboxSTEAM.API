using System.ComponentModel.DataAnnotations;
using OboxSteam.Domain.Enums;

namespace OboxSteam.Domain.Entities;

public class HighlightVideo : BaseEntity
{
    public Guid StudentId { get; set; }
    public User Student { get; set; } = null!;

    public Guid ProgramId { get; set; }
    public Program Program { get; set; } = null!;

    /// <summary>Publicly accessible URL of the finished personal video.</summary>
    public string? VideoUrl { get; set; }

    /// <summary>Legacy status field (kept for backward-compatibility).</summary>
    [MaxLength(50)]
    public string? Status { get; set; }

    // ── Personal Video Generation pipeline ──────────────────────────────────

    /// <summary>
    /// MediaConvert job ID for the stitching/clipping job.
    /// Null until a generation job has been submitted.
    /// </summary>
    [MaxLength(255)]
    public string? PersonalVideoJobRef { get; set; }

    /// <summary>Lifecycle status of the personal video generation job.</summary>
    public HighlightVideoStatus PersonalVideoStatus { get; set; } = HighlightVideoStatus.None;

    /// <summary>UTC timestamp when the generation was last triggered.</summary>
    public DateTime? PersonalVideoRequestedAt { get; set; }
}
