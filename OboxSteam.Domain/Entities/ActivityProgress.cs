using OboxSteam.Domain.Enums;

namespace OboxSteam.Domain.Entities;

/// <summary>
/// Tracks completion of a single <see cref="Activity"/> (lesson) by a student
/// within a specific module enrollment attempt. Used to evaluate the
/// "all lessons completed" condition required to pass a module.
/// Curriculum completion is based on this record reaching <see cref="ActivityStatus.Done"/>.
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

    public ActivityStatus ActivityStatus { get; set; } = ActivityStatus.NotStart;

    public bool IsCompleted { get; set; }

    public DateTime? CompletedAt { get; set; }

    /// <summary>How the student marked this activity complete (set on Done only).</summary>
    public CompletionSource? CompletionSource { get; set; }

    /// <summary>JSON resume payload (video position, PDF page, scroll ratio, etc.).</summary>
    public string? ResumeState { get; set; }

    /// <summary>Last time the student saved a learning checkpoint.</summary>
    public DateTime? LastAccessedAt { get; set; }
}
