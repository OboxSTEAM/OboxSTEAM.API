using System.ComponentModel.DataAnnotations;

namespace OboxSteam.Domain.Entities;

/// <summary>
/// Named rubric row on an immutable published framework version.
/// </summary>
public class FrameworkRubricCriterion : BaseEntity
{
    /// <summary>Legacy identity FK retained while existing rows are backfilled.</summary>
    public Guid FrameworkId { get; set; }
    public ProgramFramework Framework { get; set; } = null!;

    public Guid? FrameworkVersionId { get; set; }
    public ProgramFrameworkVersion? FrameworkVersion { get; set; }

    [MaxLength(255)]
    public string Name { get; set; } = null!;

    public string? Description { get; set; }

    /// <summary>What satisfactory evidence for this criterion looks like.</summary>
    public string? EvidenceGuidance { get; set; }

    public int MaxScore { get; set; }

    public int DisplayOrder { get; set; }

    public ICollection<ReviewCriterionScore> Scores { get; set; } = new List<ReviewCriterionScore>();
}
