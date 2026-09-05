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

    /// <summary>Geo coordinate of the Offline venue; when set, <see cref="Longitude"/> must also be set.</summary>
    public double? Latitude { get; set; }

    /// <summary>Geo coordinate of the Offline venue; when set, <see cref="Latitude"/> must also be set.</summary>
    public double? Longitude { get; set; }

    /// <summary>Rotating QR check-in token (embedded in the QR the mentor projects). Null until first generated.</summary>
    public Guid? CheckInToken { get; set; }

    /// <summary>6-digit fallback code shown next to the QR; rotates together with <see cref="CheckInToken"/>.</summary>
    [MaxLength(6)]
    public string? CheckInCode { get; set; }

    /// <summary>Expiry of the current token/code pair (short TTL to defeat shared QR screenshots).</summary>
    public DateTime? CheckInTokenExpiresAt { get; set; }

    public bool RequiresAttendance { get; set; } = true;

    /// <summary>Whether the assigned mentor must check in for this specific session instance.</summary>
    public bool RequiresMentorCheckIn { get; set; }

    public ClassSessionStatus Status { get; set; } = ClassSessionStatus.Scheduled;

    /// <summary>
    /// When the 30-minute session reminder was published. Null until
    /// <c>SessionReminderService</c> fires once for this session.
    /// </summary>
    public DateTime? ReminderSentAt { get; set; }

    // Navigation
    public ICollection<SessionAttendance> SessionAttendances { get; set; } = new List<SessionAttendance>();
    public ICollection<ClassSessionExpert> ClassSessionExperts { get; set; } = new List<ClassSessionExpert>();
}
