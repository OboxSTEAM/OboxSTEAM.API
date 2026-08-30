using OboxSteam.Domain.Enums;

namespace OboxSteam.Domain.Entities;

/// <summary>
/// Audit row for one expert curriculum-review round on a program.
/// Distinct from <see cref="ProgramReview"/> (student star ratings).
/// </summary>
public class CurriculumReview : BaseEntity
{
    public Guid ProgramId { get; set; }
    public Program Program { get; set; } = null!;

    public Guid ExpertId { get; set; }
    public Expert Expert { get; set; } = null!;

    public int Round { get; set; }

    public CurriculumReviewDecision Decision { get; set; }

    public string? Comment { get; set; }

    public DateTime ReviewedAt { get; set; }

    public ICollection<ReviewCriterionScore> CriterionScores { get; set; } = new List<ReviewCriterionScore>();
}
