using System.ComponentModel.DataAnnotations;

namespace OboxSteam.Domain.Entities;

/// <summary>
/// Named rubric row on a <see cref="ProgramFramework"/> scorecard.
/// </summary>
public class FrameworkRubricCriterion : BaseEntity
{
    public Guid FrameworkId { get; set; }
    public ProgramFramework Framework { get; set; } = null!;

    [MaxLength(255)]
    public string Name { get; set; } = null!;

    public string? Description { get; set; }

    public int MaxScore { get; set; }

    public int DisplayOrder { get; set; }

    public ICollection<ReviewCriterionScore> Scores { get; set; } = new List<ReviewCriterionScore>();
}
