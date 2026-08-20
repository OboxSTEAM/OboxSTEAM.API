using OboxSteam.Application.DTOs.ScheduleDTO;

namespace OboxSteam.Application.Interfaces;

/// <summary>
/// Weekly class timetable for a student (self) or a verified parent of that student.
/// Occupied-interval conflict data stays on <see cref="IClassEnrollmentService.GetMyScheduleAsync"/>.
/// </summary>
public interface IScheduleService
{
    /// <summary>
    /// Returns sessions for one Monday–Sunday week in Asia/Ho_Chi_Minh, grouped by local date.
    /// Omit <paramref name="weekStart"/> to use the current Monday. Cancelled sessions are omitted.
    /// Students omit <paramref name="studentId"/> (own schedule). Parents must pass a linked child id.
    /// </summary>
    Task<WeeklyScheduleResponseDto> GetWeeklyScheduleAsync(
        DateOnly? weekStart = null,
        Guid? studentId = null);
}
