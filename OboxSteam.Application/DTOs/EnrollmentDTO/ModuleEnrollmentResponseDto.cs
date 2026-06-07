using OboxSteam.Domain.Enums;

namespace OboxSteam.Application.DTOs.EnrollmentDTO;

/// <summary>
/// Module enrollment with the enrolled module's catalog information.
/// </summary>
public class ModuleEnrollmentResponseDto
{
    public Guid Id { get; set; }
    public Guid StudentId { get; set; }
    public Guid ModuleId { get; set; }
    public Guid ProgramEnrollmentId { get; set; }
    public EnrollmentStatus Status { get; set; }
    public decimal ProgressPercent { get; set; }
    public decimal? FinalGrade { get; set; }
    public int AttemptNumber { get; set; }
    public int AssignmentFailureCount { get; set; }
    public DateTime? EnrolledAt { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    public string Code { get; set; } = null!;
    public Guid ProgramId { get; set; }
    public string Name { get; set; } = null!;
    public ModuleType ModuleType { get; set; }
    public int ModuleOrder { get; set; }
    public Guid? PrerequisiteModuleId { get; set; }
    public bool IsMandatory { get; set; }
    public decimal Price { get; set; }
    public decimal RetakeFee { get; set; }
}
