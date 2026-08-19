using OboxSteam.Domain.Enums;

namespace OboxSteam.Application.DTOs.ClassEnrollmentDTO;

/// <summary>One occupied interval on the current student's class calendar.</summary>
public class StudentScheduleIntervalDto
{
    public Guid ClassSessionId { get; set; }
    public Guid ClassId { get; set; }
    public string ClassCode { get; set; } = null!;
    public string ClassName { get; set; } = null!;
    public string Title { get; set; } = null!;
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public SessionKind SessionKind { get; set; }
    public ClassSessionStatus Status { get; set; }
}
