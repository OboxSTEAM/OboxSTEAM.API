using OboxSteam.Application.Commons;
using OboxSteam.Application.DTOs.SessionAttendanceDTO;
using OboxSteam.Domain.Enums;

namespace OboxSteam.Application.Interfaces;

/// <summary>
/// Roster attendance for class sessions.
/// Only mentors, managers, and super admins may update attendance.
/// </summary>
public interface ISessionAttendanceService
{
    /// <summary>
    /// Returns attendance for a class session. Students receive only their own record;
    /// mentors, managers, and super admins receive the full roster.
    /// </summary>
    Task<Pagination<SessionAttendanceResponseDto>> GetSessionAttendancesByClassSessionIdAsync(
        Guid classSessionId,
        string? sortBy,
        bool isDescending,
        int page,
        int pageSize,
        AttendanceStatus? status = null,
        Guid? studentId = null);

    /// <summary>
    /// Records or updates attendance for a roster entry by student (upsert).
    /// <c>RecordedBy</c> is taken from the authenticated user.
    /// <c>CheckedInAt</c> is set automatically on every update.
    /// </summary>
    Task<SessionAttendanceResponseDto> UpdateSessionAttendanceAsync(
        Guid classId,
        Guid sessionId,
        Guid studentId,
        UpdateSessionAttendanceRequestDto request);
}
