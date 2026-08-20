using OboxSteam.Application.DTOs.ScheduleDTO;

namespace OboxSteam.Application.Interfaces;

/// <summary>
/// Weekly class timetable for the authenticated student.
/// Occupied-interval conflict data stays on <see cref="IClassEnrollmentService.GetMyScheduleAsync"/>.
/// </summary>
public interface IStudentScheduleService
{
    /// <summary>
    /// Returns sessions for one Monday–Sunday week in Asia/Ho_Chi_Minh, grouped by local date.
    /// Omit <paramref name="weekStart"/> to use the current Monday. Cancelled sessions are omitted.
    /// </summary>
    /// <param name="weekStart">Monday of the requested week (yyyy-MM-dd). Must be a Monday when provided.</param>
    Task<StudentWeeklyScheduleResponseDto> GetMyWeeklyScheduleAsync(DateOnly? weekStart = null);
}
