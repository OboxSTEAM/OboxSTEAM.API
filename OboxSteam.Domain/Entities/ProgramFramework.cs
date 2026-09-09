using System.ComponentModel.DataAnnotations;
using OboxSteam.Domain.Enums;

namespace OboxSteam.Domain.Entities;

/// <summary>
/// Reusable expert-authored academic framework identity.
/// </summary>
public class ProgramFramework : BaseEntity
{
    public Guid ExpertId { get; set; }
    public Expert Expert { get; set; } = null!;

    [MaxLength(255)]
    public string Name { get; set; } = null!;

    /// <summary>Hint and filter only; programs are not required to match this category.</summary>
    public ProgramCategory Category { get; set; }

    /// <summary>Legacy version-1 payload retained for additive migration compatibility.</summary>
    public string? Description { get; set; }
    public int? MinModules { get; set; }
    public int? MinOfflineSessions { get; set; }
    public int? MinLiveSessions { get; set; }
    public bool? RequireCapstoneResearchMilestone { get; set; }

    public bool IsArchived { get; set; }
    public DateTime? ArchivedAt { get; set; }

    public ICollection<ProgramFrameworkVersion> Versions { get; set; } = new List<ProgramFrameworkVersion>();
    public ICollection<FrameworkRubricCriterion> LegacyRubricCriteria { get; set; } = [];
    public ICollection<Program> Programs { get; set; } = new List<Program>();
}
