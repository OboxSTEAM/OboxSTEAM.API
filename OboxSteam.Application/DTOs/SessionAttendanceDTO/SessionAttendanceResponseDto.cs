using OboxSteam.Domain.Enums;

namespace OboxSteam.Application.DTOs.SessionAttendanceDTO;

/// <summary>
/// Roster attendance record for a student in a class session.
/// </summary>
public sealed class SessionAttendanceResponseDto
{
    public Guid Id { get; set; }
    public Guid ClassSessionId { get; set; }
    public Guid StudentId { get; set; }
    public Guid ModuleEnrollmentId { get; set; }
    public AttendanceStatus Status { get; set; }
    public DateTime? CheckedInAt { get; set; }
    public Guid? RecordedBy { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
