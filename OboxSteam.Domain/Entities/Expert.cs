using System.ComponentModel.DataAnnotations;
using OboxSteam.Domain.Enums;

namespace OboxSteam.Domain.Entities;

/// <summary>
/// Expert profile. New experts are provisioned with a dedicated <see cref="RoleType.Expert"/> login.
/// <see cref="UserId"/> stays nullable for legacy rows until seed/upgrade links them.
/// </summary>
public class Expert : BaseEntity
{
    [MaxLength(50)]
    public string Code { get; set; } = null!; // e.g., EXP-001

    /// <summary>Linked <see cref="User"/> with <c>RoleType.Expert</c>. Required for newly created experts.</summary>
    public Guid? UserId { get; set; }
    public User? User { get; set; }

    [MaxLength(255)]
    public string FullName { get; set; } = null!;

    [MaxLength(255)]
    public string? Title { get; set; } // e.g., Professor of Robotics, PhD in AI

    [MaxLength(255)]
    public string? Organization { get; set; }

    public string? Bio { get; set; }

    public string? AvatarUrl { get; set; }

    public string? LinkedInUrl { get; set; }

    public string? Achievements { get; set; }

    /// <summary>Free-form specialization tags (e.g. Robotics, AI education).</summary>
    public string[] Specialization { get; set; } = Array.Empty<string>();

    // Navigation
    public ICollection<ProgramBoard> ProgramBoards { get; set; } = new List<ProgramBoard>();
    public ICollection<ExpertDegree> Degrees { get; set; } = new List<ExpertDegree>();
    public ICollection<ExpertPublication> Publications { get; set; } = new List<ExpertPublication>();
    public ICollection<ProgramFramework> ProgramFrameworks { get; set; } = new List<ProgramFramework>();
    public ICollection<CurriculumReview> CurriculumReviews { get; set; } = new List<CurriculumReview>();
    public ICollection<ClassSessionExpert> ClassSessionExperts { get; set; } = new List<ClassSessionExpert>();
}
