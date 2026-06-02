using System.ComponentModel.DataAnnotations;
using OboxSteam.Domain.Enums;

namespace OboxSteam.Domain.Entities;

public class Material : BaseEntity
{
    public Guid ModuleId { get; set; }
    public Module Module { get; set; } = null!;

    /// <summary>Specific course within the module (optional).</summary>
    public Guid? CourseId { get; set; }
    public Course? Course { get; set; }

    /// <summary>Null if the material belongs to the module directly (not a specific activity).</summary>
    public Guid? ActivityId { get; set; }
    public Activity? Activity { get; set; }

    [MaxLength(255)]
    public string Title { get; set; } = null!;

    public MaterialType MaterialType { get; set; }

    public string? FileUrl { get; set; }

    /// <summary>Original file size in bytes.</summary>
    public long? FileSizeBytes { get; set; }
}
