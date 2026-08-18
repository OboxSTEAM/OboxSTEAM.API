using System.ComponentModel.DataAnnotations;
using OboxSteam.Domain.Enums;

namespace OboxSteam.Domain.Entities;

/// <summary>
/// Activities represent individual learning tasks within a course.
/// Can be SelfPaced (no scheduling), LiveOnline, or Offline.
/// StartTime/EndTime/Location are template defaults; actual cohort
/// schedule lives on <see cref="ClassSession"/>.
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

    // Template scheduling hints — cohort times are on ClassSession
    [MaxLength(500)]
    public string? Location { get; set; } // Google Meet link or physical address

    public DateTime? StartTime { get; set; }
    public DateTime? EndTime { get; set; }

    public bool RequireQrCheckin { get; set; } // True only for Offline
    public bool RequireMediaEvidence { get; set; }

    // Navigation
    public Material? Material { get; set; }
    public ICollection<ActivityProgress> ActivityProgresses { get; set; } = new List<ActivityProgress>();
    public ICollection<ClassSession> ClassSessions { get; set; } = new List<ClassSession>();
    public ICollection<ResearchMilestoneActivity> ResearchMilestoneActivities { get; set; } =
        new List<ResearchMilestoneActivity>();
}
