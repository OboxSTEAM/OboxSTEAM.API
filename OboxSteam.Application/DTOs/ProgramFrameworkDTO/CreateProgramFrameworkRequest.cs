using System.ComponentModel.DataAnnotations;
using OboxSteam.Domain.Enums;

namespace OboxSteam.Application.DTOs.ProgramFrameworkDTO;

/// <summary>
/// Creates a framework with draft version 1. Every rule is opt-in:
/// null or false = not enforced.
/// </summary>
public class CreateProgramFrameworkRequest
{
    [Required]
    [MaxLength(255)]
    public string Name { get; set; } = null!;

    public string? Description { get; set; }

    public string? AcademicGuidance { get; set; }

    public ProgramCategory Category { get; set; }

    public int? MinModules { get; set; }

    public int? MaxModules { get; set; }

    public int? MinCoursesPerModule { get; set; }

    public int? MaxCoursesPerModule { get; set; }

    public int? MinTotalHours { get; set; }

    public int? MaxTotalHours { get; set; }

    public int? MaxActivityMinutes { get; set; }

    public bool RequireActivityDuration { get; set; }

    public int? MinOfflineSessions { get; set; }

    public int? MinLiveSessions { get; set; }

    /// <summary>Minimum share (0–100) of activities that are Offline.</summary>
    public int? MinOfflineRatioPercent { get; set; }

    /// <summary>Minimum share (0–100) of activities that are LiveOnline.</summary>
    public int? MinLiveRatioPercent { get; set; }

    public bool RequireAssignmentPerModule { get; set; }

    public bool RequireAssignmentPassScore { get; set; }

    /// <summary>Minimum materials on each SelfPaced activity.</summary>
    public int? MinMaterialsPerActivity { get; set; }

    public bool RequireCategoryMatch { get; set; }

    public int? MinDescriptionLength { get; set; }

    /// <summary>Minimum linked program skills.</summary>
    public int? MinSkillsGained { get; set; }

    public bool RequireThumbnail { get; set; }

    /// <summary>
    /// When true, the program needs ≥1 ResearchMilestone with IsCapstone.
    /// Null or false is not enforced.
    /// </summary>
    public bool? RequireCapstoneResearchMilestone { get; set; }
}
