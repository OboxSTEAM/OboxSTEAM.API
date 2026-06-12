namespace OboxSteam.Domain.Entities;

/// <summary>
/// Links an <see cref="Activity"/> to a <see cref="ResearchMilestone"/>.
/// Optional activities (e.g. reading, video) may be skipped before submission.
/// </summary>
public class ResearchMilestoneActivity : BaseEntity
{
    public Guid ResearchMilestoneId { get; set; }
    public ResearchMilestone ResearchMilestone { get; set; } = null!;

    public Guid ActivityId { get; set; }
    public Activity Activity { get; set; } = null!;

    /// <summary>
    /// When true, the student must complete this activity before submitting the milestone deliverable.
    /// </summary>
    public bool IsRequiredForSubmission { get; set; }

    public int DisplayOrder { get; set; }
}
