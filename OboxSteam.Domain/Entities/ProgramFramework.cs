using System.ComponentModel.DataAnnotations;
using OboxSteam.Domain.Enums;

namespace OboxSteam.Domain.Entities;

/// <summary>
/// Expert-owned curriculum blueprint for a content family.
/// Opt-in rules: a null constraint is not enforced at submit-review.
/// </summary>
public class ProgramFramework : BaseEntity
{
    public Guid ExpertId { get; set; }
    public Expert Expert { get; set; } = null!;

    [MaxLength(255)]
    public string Name { get; set; } = null!;

    public string? Description { get; set; }

    /// <summary>Hint and filter only; programs are not required to match this category.</summary>
    public ProgramCategory Category { get; set; }

    public int? MinModules { get; set; }

    public int? MinOfflineSessions { get; set; }

    public int? MinLiveSessions { get; set; }

    public bool? RequireFinalAssessment { get; set; }

    public ICollection<FrameworkRubricCriterion> RubricCriteria { get; set; } = new List<FrameworkRubricCriterion>();
    public ICollection<Program> Programs { get; set; } = new List<Program>();
}
