namespace OboxSteam.Application.DTOs.AssessmentRecoveryDTO;

public class CreateAssessmentRecoveryRequestDto
{
    public Guid ModuleEnrollmentId { get; set; }
    public Guid AssignmentId { get; set; }
    public string? StudentMessage { get; set; }
}
