using System.ComponentModel.DataAnnotations;
using OboxSteam.Domain.Enums;

namespace OboxSteam.Domain.Entities;

public class Program : BaseEntity
{
    [MaxLength(50)]
    public string Code { get; set; } = null!; // e.g., PRG-ROBOTICS

    [MaxLength(255)]
    public string Name { get; set; } = null!;

    [MaxLength(255)]
    public string? SeriesName { get; set; } // e.g., Obox Master Track

    public string? Description { get; set; }

    public DifficultyLevel Level { get; set; } = DifficultyLevel.Beginner;

    [MaxLength(255)]
    public string? EstimatedDuration { get; set; } // e.g., 3 months at 10 hours a week

    public string? SkillsGained { get; set; } // JSON array or comma separated

    public decimal? Rating { get; set; } // e.g., 4.8

    public int TotalReviews { get; set; }

    public string? ThumbnailUrl { get; set; }

    [MaxLength(50)]
    public string? Status { get; set; }

    /// <summary>Bundle price for the entire program (usually discounted vs sum of modules).</summary>
    public decimal? Price { get; set; }

    // Navigation
    public ICollection<ProgramBoard> ProgramBoards { get; set; } = new List<ProgramBoard>();
    public ICollection<Module> Modules { get; set; } = new List<Module>();
    public ICollection<ProgramEnrollment> ProgramEnrollments { get; set; } = new List<ProgramEnrollment>();
    public ICollection<Certificate> Certificates { get; set; } = new List<Certificate>();
    public ICollection<HighlightVideo> HighlightVideos { get; set; } = new List<HighlightVideo>();
}
