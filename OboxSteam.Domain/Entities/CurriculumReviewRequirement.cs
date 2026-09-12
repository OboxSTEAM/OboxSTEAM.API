namespace OboxSteam.Domain.Entities;

/// <summary>Links a formal decision to the required-change threads it references.</summary>
public sealed class CurriculumReviewRequirement : BaseEntity
{
    public Guid ProgramId { get; set; }
    public Program Program { get; set; } = null!;
    public Guid CurriculumReviewId { get; set; }
    public CurriculumReview CurriculumReview { get; set; } = null!;
    public Guid ThreadId { get; set; }
    public ProgramAdvisoryThread Thread { get; set; } = null!;
}
