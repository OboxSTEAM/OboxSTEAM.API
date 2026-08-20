using OboxSteam.Domain.Enums;

namespace OboxSteam.Application.DTOs.ScheduleDTO;

/// <summary>One class session cell on the student's weekly timetable.</summary>
public sealed class StudentScheduleSessionDto
{
    public Guid Id { get; set; }
    public Guid ClassId { get; set; }
    public Guid ProgramId { get; set; }
    public Guid? MentorId { get; set; }
    public Guid ModuleId { get; set; }
    public Guid? ActivityId { get; set; }
    public SessionKind SessionKind { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public string? Location { get; set; }
    public string? MeetingUrl { get; set; }

    /// <summary>
    /// Session lifecycle. <see cref="ClassSessionStatus.Completed"/> means the session has finished.
    /// </summary>
    public ClassSessionStatus Status { get; set; }

    /// <summary>
    /// True when <see cref="Status"/> is <see cref="ClassSessionStatus.Completed"/>.
    /// </summary>
    public bool IsCompleted { get; set; }

    /// <summary>
    /// Student roster status for this session. Null when no attendance row exists.
    /// </summary>
    public AttendanceStatus? AttendanceStatus { get; set; }

    /// <summary>
    /// True when the student checked in (<see cref="AttendanceStatus.Present"/> or <see cref="AttendanceStatus.Late"/>).
    /// </summary>
    public bool HasCheckedIn { get; set; }
}
