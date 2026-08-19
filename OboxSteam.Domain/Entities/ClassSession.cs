using System.ComponentModel.DataAnnotations;
using OboxSteam.Domain.Enums;

namespace OboxSteam.Domain.Entities;

/// <summary>
/// A scheduled calendar event for a cohort — lesson, field trip, live session, or assignment window.
/// At least one of ActivityId or AssignmentId must be set (enforced in application layer).
/// </summary>
public class ClassSession : BaseEntity
{
    public Guid ClassId { get; set; }
    public Class Class { get; set; } = null!;

    public Guid ModuleId { get; set; }
    public Module Module { get; set; } = null!;

    public Guid? ActivityId { get; set; }
    public Activity? Activity { get; set; }

    public Guid? AssignmentId { get; set; }
    public Assignment? Assignment { get; set; }

    public SessionKind SessionKind { get; set; }

    [MaxLength(255)]
    public string Title { get; set; } = null!;

    public string? Description { get; set; }

    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }

    [MaxLength(500)]
    public string? Location { get; set; }

    /// <summary>Join URL for LiveOnline sessions (kept separate from free-text <see cref="Location"/>).</summary>
    [MaxLength(2048)]
    public string? MeetingUrl { get; set; }

    public bool RequiresAttendance { get; set; } = true;

    /// <summary>Whether the assigned mentor must check in for this specific session instance.</summary>
    public bool RequiresMentorCheckIn { get; set; }

    public ClassSessionStatus Status { get; set; } = ClassSessionStatus.Scheduled;

    // Navigation
    public ICollection<SessionAttendance> SessionAttendances { get; set; } = new List<SessionAttendance>();
}
