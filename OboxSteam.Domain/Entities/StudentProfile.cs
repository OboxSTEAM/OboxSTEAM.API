using System.ComponentModel.DataAnnotations;

namespace OboxSteam.Domain.Entities;

/// <summary>
/// Student academic profile — 1:1 with User (student).
/// Uses StudentId as both PK and FK.
/// </summary>
public class StudentProfile : BaseEntity
{
    public Guid StudentId { get; set; }
    public User Student { get; set; } = null!;

    [MaxLength(255)]
    public string? CurrentSchool { get; set; }

    public int? GradeLevel { get; set; }

    [MaxLength(255)]
    public string? CareerOrientation { get; set; }

    [MaxLength(255)]
    public string? TargetCountry { get; set; }

    public int? IntendedYear { get; set; }

    public decimal? GpaOverall { get; set; }

    public decimal? GpaStem { get; set; }

    public string? PersonalStatement { get; set; }
}
