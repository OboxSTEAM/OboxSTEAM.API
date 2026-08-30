using OboxSteam.Domain.Enums;

namespace OboxSteam.Application.DTOs.AssessmentRecoveryDTO;

public class AssessmentRecoveryRequestResponseDto
{
    public Guid Id { get; set; }
    public Guid StudentId { get; set; }
    public Guid ModuleEnrollmentId { get; set; }
    public Guid AssignmentId { get; set; }
    public Guid? ClassId { get; set; }
    public AssessmentRecoveryRequestStatus Status { get; set; }
    public string? StudentMessage { get; set; }
    public string? MentorNote { get; set; }
    public int ExtraAttemptsGranted { get; set; }
    public DateTime? DecidedAt { get; set; }
    public Guid? DecidedBy { get; set; }
    public DateTime CreatedAt { get; set; }
}
