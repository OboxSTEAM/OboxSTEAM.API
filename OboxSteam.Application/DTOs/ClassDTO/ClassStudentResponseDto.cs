using OboxSteam.Domain.Enums;

namespace OboxSteam.Application.DTOs.ClassDTO;

/// <summary>
/// One student row for <c>GET /api/classes/with-students/{classId}</c>.
/// Class context is provided by the route; this DTO carries enrollment and student display fields only.
/// </summary>
public class ClassStudentResponseDto
{
    public Guid ClassEnrollmentId { get; set; }
    public Guid StudentId { get; set; }
    public string StudentCode { get; set; } = null!;
    public string? StudentName { get; set; }
    public string Email { get; set; } = null!;
    public string? Phone { get; set; }
    public string? AvatarUrl { get; set; }
    public ClassEnrollmentStatus EnrollmentStatus { get; set; }
    public DateTime? EnrolledAt { get; set; }
}
