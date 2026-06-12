using System.ComponentModel.DataAnnotations;

namespace OboxSteam.Domain.Entities;

/// <summary>
/// A checkpoint within a <see cref="Module"/> of type Research.
/// Each milestone owns one graded <see cref="Assignment"/> deliverable and
/// optionally links <see cref="Activity"/> records via <see cref="ResearchMilestoneActivity"/>.
/// </summary>
public class ResearchMilestone : BaseEntity
{
    [MaxLength(50)]
    public string Code { get; set; } = null!;

    public Guid ModuleId { get; set; }
    public Module Module { get; set; } = null!;

    [MaxLength(255)]
    public string Title { get; set; } = null!;

    public string? Description { get; set; }

    /// <summary>Sequence within the module; used for unlock ordering.</summary>
    public int MilestoneOrder { get; set; }

    /// <summary>When true, this is the final capstone milestone for the module.</summary>
    public bool IsCapstone { get; set; }

    /// <summary>The graded deliverable students submit for this milestone.</summary>
    public Guid AssignmentId { get; set; }
    public Assignment Assignment { get; set; } = null!;

    public ICollection<ResearchMilestoneActivity> MilestoneActivities { get; set; } =
        new List<ResearchMilestoneActivity>();

    public ICollection<Submission> Submissions { get; set; } = new List<Submission>();
}
