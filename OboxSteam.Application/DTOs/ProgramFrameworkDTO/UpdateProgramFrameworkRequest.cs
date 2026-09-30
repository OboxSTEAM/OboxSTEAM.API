using System.ComponentModel.DataAnnotations;
using OboxSteam.Domain.Enums;

namespace OboxSteam.Application.DTOs.ProgramFrameworkDTO;

/// <summary>
/// Partial update of the draft version. A null value leaves the field unchanged;
/// set <c>Clear{Field}</c> to turn a numeric rule off (ignored when the value is set).
/// Boolean rules are set explicitly (null = unchanged).
/// </summary>
public class UpdateProgramFrameworkRequest
{
    [MaxLength(255)]
    public string? Name { get; set; }

    public string? Description { get; set; }

    public string? AcademicGuidance { get; set; }

    public ProgramCategory? Category { get; set; }

    public int? MinModules { get; set; }

    public bool? ClearMinModules { get; set; }

    public int? MaxModules { get; set; }

    public bool? ClearMaxModules { get; set; }

    public int? MinCoursesPerModule { get; set; }

    public bool? ClearMinCoursesPerModule { get; set; }

    public int? MaxCoursesPerModule { get; set; }

    public bool? ClearMaxCoursesPerModule { get; set; }

    public int? MinTotalHours { get; set; }

    public bool? ClearMinTotalHours { get; set; }

    public int? MaxTotalHours { get; set; }

    public bool? ClearMaxTotalHours { get; set; }

    public int? MaxActivityMinutes { get; set; }

    public bool? ClearMaxActivityMinutes { get; set; }

    public bool? RequireActivityDuration { get; set; }

    public int? MinOfflineSessions { get; set; }

    public bool? ClearMinOfflineSessions { get; set; }

    public int? MinLiveSessions { get; set; }

    public bool? ClearMinLiveSessions { get; set; }

    public int? MinOfflineRatioPercent { get; set; }

    public bool? ClearMinOfflineRatioPercent { get; set; }

    public int? MinLiveRatioPercent { get; set; }

    public bool? ClearMinLiveRatioPercent { get; set; }

    public bool? RequireAssignmentPerModule { get; set; }

    public bool? RequireAssignmentPassScore { get; set; }

    public int? MinMaterialsPerActivity { get; set; }

    public bool? ClearMinMaterialsPerActivity { get; set; }

    public bool? RequireCategoryMatch { get; set; }

    public int? MinDescriptionLength { get; set; }

    public bool? ClearMinDescriptionLength { get; set; }

    public int? MinSkillsGained { get; set; }

    public bool? ClearMinSkillsGained { get; set; }

    public bool? RequireThumbnail { get; set; }

    /// <summary>
    /// When true, the program needs ≥1 ResearchMilestone with IsCapstone.
    /// Null or false is not enforced.
    /// </summary>
    public bool? RequireCapstoneResearchMilestone { get; set; }

    /// <summary>
    /// When true, clears <c>RequireCapstoneResearchMilestone</c> (null = not enforced).
    /// Ignored when <see cref="RequireCapstoneResearchMilestone"/> is set.
    /// </summary>
    public bool? ClearRequireCapstoneResearchMilestone { get; set; }
}
