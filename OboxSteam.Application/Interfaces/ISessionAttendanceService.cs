using OboxSteam.Application.Commons;
using OboxSteam.Application.DTOs.SessionAttendanceDTO;
using OboxSteam.Domain.Enums;

namespace OboxSteam.Application.Interfaces;

/// <summary>
/// Roster attendance for class sessions. Records are auto-created when a session is published.
/// Students may only view their own attendance records; mentors and admins may view the full roster.
/// Only mentors, managers, and super admins may update attendance.
/// </summary>
public interface ISessionAttendanceService
{
    /// <summary>
    /// Returns one attendance record. Students may only retrieve their own row.
    /// </summary>
    Task<SessionAttendanceResponseDto> GetSessionAttendanceByIdAsync(Guid id);

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
        AttendanceStatus? status = null);

    /// <summary>
    /// Records or updates attendance for a roster entry. Only Mentor, Manager, and SuperAdmin may call this.
    /// <c>RecordedBy</c> is taken from the authenticated user.
    /// </summary>
    Task<SessionAttendanceResponseDto> UpdateSessionAttendanceAsync(
        Guid id,
        UpdateSessionAttendanceRequestDto request);

    /// <summary>
    /// Creates roster attendance rows for enrolled students in the session's class cohort.
    /// Intended to run when a <see cref="Domain.Entities.ClassSession"/> is published.
    /// </summary>
    Task<List<SessionAttendanceResponseDto>> GenerateSessionAttendanceRosterAsync(Guid classSessionId);
}
