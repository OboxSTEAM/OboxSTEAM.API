using OboxSteam.Application.Commons;
using OboxSteam.Application.DTOs.ClassSessionDTO;
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

    /// <summary>
    /// Generates (or rotates) the QR check-in token and 6-digit fallback code for a session.
    /// Only the assigned mentor (or Manager/Admin) may generate; the pair expires after a short TTL.
    /// </summary>
    Task<ClassSessionCheckInTokenResponseDto> GenerateCheckInTokenAsync(Guid classSessionId);

    /// <summary>
    /// Student self check-in via QR token or 6-digit code; records attendance as Present
    /// with <c>RecordedBy</c> set to the student themself.
    /// </summary>
    Task<SessionAttendanceResponseDto> CheckInAsync(Guid classSessionId, ClassSessionCheckInRequestDto request);
}
