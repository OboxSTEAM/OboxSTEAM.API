namespace OboxSteam.Domain.Entities;

/// <summary>
/// Tracks completion of a single <see cref="Activity"/> (lesson) by a student
/// within a specific module enrollment attempt. Used to evaluate the
/// "all lessons completed" condition required to pass a module.
/// For Live/Offline activities, completion is driven by attendance
/// (<see cref="ActivityBooking"/>); this entity primarily covers SelfPaced activities.
/// </summary>
public class ActivityProgress : BaseEntity
{
    public Guid StudentId { get; set; }
    public User Student { get; set; } = null!;

    public Guid ActivityId { get; set; }
    public Activity Activity { get; set; } = null!;

    /// <summary>Anchors the progress to the module enrollment attempt it belongs to.</summary>
    public Guid ModuleEnrollmentId { get; set; }
    public ModuleEnrollment ModuleEnrollment { get; set; } = null!;

    public bool IsCompleted { get; set; }

    public DateTime? CompletedAt { get; set; }
}
