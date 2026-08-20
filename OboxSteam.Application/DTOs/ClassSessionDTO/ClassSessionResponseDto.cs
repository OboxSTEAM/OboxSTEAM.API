using OboxSteam.Domain.Enums;

namespace OboxSteam.Application.DTOs.ClassSessionDTO;

public class ClassSessionResponseDto
{
    public Guid Id { get; set; }
    public Guid ClassId { get; set; }
    public Guid ModuleId { get; set; }
    public Guid? ActivityId { get; set; }
    public Guid? AssignmentId { get; set; }
    public SessionKind SessionKind { get; set; }
    public string Title { get; set; } = null!;
    public string? Description { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public string? Location { get; set; }
    public string? MeetingUrl { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public bool RequiresAttendance { get; set; }
    public bool RequiresMentorCheckIn { get; set; }
    public ClassSessionStatus Status { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
