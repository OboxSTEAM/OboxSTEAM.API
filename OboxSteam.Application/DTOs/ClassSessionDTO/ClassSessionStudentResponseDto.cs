using OboxSteam.Domain.Enums;

namespace OboxSteam.Application.DTOs.ClassSessionDTO;

/// <summary>
/// One student row for a class session roster, including attendance and display fields.
/// </summary>
public class ClassSessionStudentResponseDto
{
    public Guid ClassSessionId { get; set; }
    public Guid StudentId { get; set; }
    public string StudentCode { get; set; } = null!;
    public string? StudentName { get; set; }
    public string Email { get; set; } = null!;
    public string? Phone { get; set; }
    public string? AvatarUrl { get; set; }
    public Guid ModuleEnrollmentId { get; set; }
    public AttendanceStatus AttendanceStatus { get; set; }
    public DateTime? CheckedInAt { get; set; }
    public DateTime? LeftAt { get; set; }
    public int? ParticipationMinutes { get; set; }
    public Guid? RecordedBy { get; set; }
}
