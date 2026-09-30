using OboxSteam.Domain.Enums;

namespace OboxSteam.Application.DTOs.ProgramFrameworkDTO;

/// <summary>Framework identity with the current version's rules flattened on top.</summary>
public class ProgramFrameworkResponseDto
{
    public Guid Id { get; set; }
    public Guid ExpertId { get; set; }
    public string? ExpertName { get; set; }
    public string Name { get; set; } = null!;
    public string? Description { get; set; }
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
    public int? MinOfflineRatioPercent { get; set; }
    public int? MinLiveRatioPercent { get; set; }
    public bool RequireAssignmentPerModule { get; set; }
    public bool RequireAssignmentPassScore { get; set; }
    public int? MinMaterialsPerActivity { get; set; }
    public bool RequireCategoryMatch { get; set; }
    public int? MinDescriptionLength { get; set; }
    public int? MinSkillsGained { get; set; }
    public bool RequireThumbnail { get; set; }

    /// <summary>
    /// When true, the program needs ≥1 ResearchMilestone with IsCapstone.
    /// Null or false is not enforced.
    /// </summary>
    public bool? RequireCapstoneResearchMilestone { get; set; }

    /// <summary>
    /// Always true: submit still requires expert review (framework owner when
    /// attached; program-board experts when there is no framework).
    /// </summary>
    public bool RequiresExpertReview { get; set; }
    public bool IsArchived { get; set; }
    public Guid? CurrentVersionId { get; set; }
    public int? CurrentVersionNumber { get; set; }
    public bool HasDraftVersion { get; set; }

    public List<ProgramFrameworkVersionResponseDto> Versions { get; set; } = [];
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
