using System.ComponentModel.DataAnnotations;

namespace OboxSteam.Domain.Entities;

public class Material : BaseEntity
{
    public Guid ModuleId { get; set; }
    public Module Module { get; set; } = null!;

    /// <summary>Null if the material belongs to the module directly (not a specific activity).</summary>
    public Guid? ActivityId { get; set; }
    public Activity? Activity { get; set; }

    [MaxLength(255)]
    public string Title { get; set; } = null!;

    [MaxLength(50)]
    public string MaterialType { get; set; } = null!; // Video, PDF, ExternalLink

    public string? FileUrl { get; set; }
}
