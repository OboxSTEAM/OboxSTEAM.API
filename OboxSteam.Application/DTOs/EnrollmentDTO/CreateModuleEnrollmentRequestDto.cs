using System.ComponentModel.DataAnnotations;

namespace OboxSteam.Application.DTOs.EnrollmentDTO;

/// <summary>
/// Request to enroll in a module within an active program enrollment.
/// Business rule: if the module has a <c>PrerequisiteModuleId</c>, that prerequisite module
/// must be passed (completed) before this enrollment is allowed.
/// Prerequisite validation will be enforced in the service layer.
/// </summary>
public class CreateModuleEnrollmentRequestDto
{
    [Required(ErrorMessage = "ProgramEnrollmentId is required.")]
    public Guid ProgramEnrollmentId { get; set; }

    [Required(ErrorMessage = "ModuleId is required.")]
    public Guid ModuleId { get; set; }
}
