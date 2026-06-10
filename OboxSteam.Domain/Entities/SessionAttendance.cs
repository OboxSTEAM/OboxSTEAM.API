using OboxSteam.Domain.Enums;

namespace OboxSteam.Domain.Entities;

/// <summary>
/// Roster-based attendance for a class session. Auto-created when a session is published.
/// </summary>
public class SessionAttendance : BaseEntity
{
    public Guid ClassSessionId { get; set; }
    public ClassSession ClassSession { get; set; } = null!;

    public Guid StudentId { get; set; }
    public User Student { get; set; } = null!;

    public Guid ModuleEnrollmentId { get; set; }
    public ModuleEnrollment ModuleEnrollment { get; set; } = null!;

    public AttendanceStatus Status { get; set; } = AttendanceStatus.Expected;

    public DateTime? CheckedInAt { get; set; }

    public Guid? RecordedBy { get; set; }
    public User? Recorder { get; set; }
}
