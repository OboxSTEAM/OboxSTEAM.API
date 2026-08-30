using System.ComponentModel.DataAnnotations;

namespace OboxSteam.Domain.Entities;

/// <summary>
/// Expert score for one rubric criterion on a <see cref="CurriculumReview"/>.
/// </summary>
public class ReviewCriterionScore : BaseEntity
{
    public Guid CurriculumReviewId { get; set; }
    public CurriculumReview CurriculumReview { get; set; } = null!;

    public Guid FrameworkRubricCriterionId { get; set; }
    public FrameworkRubricCriterion FrameworkRubricCriterion { get; set; } = null!;

    public int Score { get; set; }

    [MaxLength(2000)]
    public string? Comment { get; set; }
}
