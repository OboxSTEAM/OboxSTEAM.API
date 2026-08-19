using System.ComponentModel.DataAnnotations;

namespace OboxSteam.Domain.Entities;

/// <summary>
/// Academic credential on an <see cref="Expert"/> profile (degree, institution, year).
/// </summary>
public class ExpertDegree : BaseEntity
{
    public Guid ExpertId { get; set; }
    public Expert Expert { get; set; } = null!;

    [MaxLength(255)]
    public string Title { get; set; } = null!;

    [MaxLength(255)]
    public string Institution { get; set; } = null!;

    public int Year { get; set; }
}
