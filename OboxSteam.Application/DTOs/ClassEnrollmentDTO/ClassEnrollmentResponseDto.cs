using OboxSteam.Application.DTOs.ClassDTO;
using OboxSteam.Domain.Enums;

namespace OboxSteam.Application.DTOs.ClassEnrollmentDTO;

/// <summary>
/// Student class enrollment with the selected cohort details.
/// </summary>
public class ClassEnrollmentResponseDto
{
    public Guid Id { get; set; }
    public Guid StudentId { get; set; }
    public Guid ProgramEnrollmentId { get; set; }
    public ClassEnrollmentStatus Status { get; set; }
    public DateTime? EnrolledAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    /// <summary>The class cohort the student is enrolled in.</summary>
    public ClassResponseDto Class { get; set; } = null!;
}
