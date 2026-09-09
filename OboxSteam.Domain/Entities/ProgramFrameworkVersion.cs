namespace OboxSteam.Domain.Entities;

/// <summary>
/// Versioned academic guidance. Drafts are editable; publishing makes the
/// complete version and rubric immutable.
/// </summary>
public sealed class ProgramFrameworkVersion : BaseEntity
{
    public Guid FrameworkId { get; set; }
    public ProgramFramework Framework { get; set; } = null!;

    public int VersionNumber { get; set; }

    public string? Description { get; set; }

    public string? AcademicGuidance { get; set; }

    public int? MinModules { get; set; }

    public int? MinOfflineSessions { get; set; }

    public int? MinLiveSessions { get; set; }

    public bool? RequireCapstoneResearchMilestone { get; set; }

    public bool IsPublished { get; set; }

    public DateTime? PublishedAt { get; set; }

    public ICollection<FrameworkRubricCriterion> RubricCriteria { get; set; } = [];
    public ICollection<Program> Programs { get; set; } = [];
}
