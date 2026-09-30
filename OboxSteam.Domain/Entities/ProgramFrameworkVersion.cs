namespace OboxSteam.Domain.Entities;

/// <summary>
/// Versioned academic guidance. Drafts are editable; publishing makes the
/// complete version immutable. Every rule is opt-in: null or false = not enforced.
/// </summary>
public sealed class ProgramFrameworkVersion : BaseEntity
{
    public Guid FrameworkId { get; set; }
    public ProgramFramework Framework { get; set; } = null!;

    public int VersionNumber { get; set; }

    public string? Description { get; set; }

    public string? AcademicGuidance { get; set; }

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

    /// <summary>Minimum linked <see cref="ProgramSkill"/> rows.</summary>
    public int? MinSkillsGained { get; set; }

    public bool RequireThumbnail { get; set; }

    public bool? RequireCapstoneResearchMilestone { get; set; }

    public bool IsPublished { get; set; }

    public DateTime? PublishedAt { get; set; }

    public ICollection<Program> Programs { get; set; } = [];
}
