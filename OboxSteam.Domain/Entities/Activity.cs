using System.ComponentModel.DataAnnotations;
using OboxSteam.Domain.Enums;

namespace OboxSteam.Domain.Entities;

/// <summary>
/// Activities represent individual learning tasks within a course.
/// Can be SelfPaced (no scheduling), LiveOnline, or Offline.
/// Activities are curriculum templates: where and when a cohort actually meets lives on
/// <see cref="ClassSession"/>; an activity only declares how long a session of it lasts.
/// </summary>
public class Activity : BaseEntity
{
    [MaxLength(50)]
    public string Code { get; set; } = null!;

    public Guid CourseId { get; set; }
    public Course Course { get; set; } = null!;

    [MaxLength(255)]
    public string Name { get; set; } = null!;

    public ActivityType ActivityType { get; set; }

    public string? Description { get; set; }

    public int ActivityOrder { get; set; }

    /// <summary>
    /// How long one session of this activity lasts, in minutes. Required for
    /// LiveOnline/Offline (drives <see cref="ClassSession.EndTime"/> when generating
    /// sessions); must be null for SelfPaced activities, which are never scheduled.
    /// </summary>
    public int? DurationMinutes { get; set; }

    public bool RequireQrCheckin { get; set; } // True only for Offline
    public bool RequireMediaEvidence { get; set; }

    // Navigation
    public Material? Material { get; set; }
    public ICollection<ActivityProgress> ActivityProgresses { get; set; } = new List<ActivityProgress>();
    public ICollection<ClassSession> ClassSessions { get; set; } = new List<ClassSession>();
    public ICollection<ResearchMilestoneActivity> ResearchMilestoneActivities { get; set; } =
        new List<ResearchMilestoneActivity>();
}
