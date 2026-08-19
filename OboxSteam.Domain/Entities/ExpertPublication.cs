using System.ComponentModel.DataAnnotations;

namespace OboxSteam.Domain.Entities;

/// <summary>
/// Scholarly work on an <see cref="Expert"/> profile (paper, journal/conference, year, url).
/// </summary>
public class ExpertPublication : BaseEntity
{
    public Guid ExpertId { get; set; }
    public Expert Expert { get; set; } = null!;

    [MaxLength(500)]
    public string Title { get; set; } = null!;

    [MaxLength(255)]
    public string? Venue { get; set; }

    public int Year { get; set; }

    [MaxLength(2048)]
    public string? Url { get; set; }
}
