using OboxSteam.Domain.Enums;

namespace OboxSteam.Application.DTOs.ClassSessionDTO;

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
    public bool? RequiresAttendance { get; set; }
    public bool? RequiresMentorCheckIn { get; set; }
    public ClassSessionStatus? Status { get; set; }
}
