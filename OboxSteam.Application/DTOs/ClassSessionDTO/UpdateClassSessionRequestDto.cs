using OboxSteam.Domain.Enums;

namespace OboxSteam.Application.DTOs.ClassSessionDTO;

/// <summary>
/// Partial update for a class session.
/// Activity-backed sessions: change <see cref="StartTime"/> to reschedule — <see cref="EndTime"/>
/// is always recomputed from the activity's DurationMinutes and cannot be set directly.
/// Assignment sessions: <see cref="StartTime"/> / <see cref="EndTime"/> may both be changed.
/// </summary>
public class UpdateClassSessionRequestDto
{
    public Guid? ModuleId { get; set; }
    public Guid? ActivityId { get; set; }
    public Guid? AssignmentId { get; set; }
    public SessionKind? SessionKind { get; set; }
    public string? Title { get; set; }
    public string? Description { get; set; }
    public DateTime? StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public string? Location { get; set; }
    public string? MeetingUrl { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public bool? RequiresAttendance { get; set; }
    public bool? RequiresMentorCheckIn { get; set; }
    public ClassSessionStatus? Status { get; set; }
}
