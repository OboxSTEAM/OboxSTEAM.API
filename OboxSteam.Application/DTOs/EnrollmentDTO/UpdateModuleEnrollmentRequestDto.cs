using System.ComponentModel.DataAnnotations;

namespace OboxSteam.Application.DTOs.EnrollmentDTO;

/// <summary>
/// Request to re-enroll in a module after failing or not passing.
/// Business rule: student must have completed the module retake payment before re-enrollment is allowed.
/// Payment validation will be enforced in the service layer when payment is implemented.
/// </summary>
public class UpdateModuleEnrollmentRequestDto
{
    [Required(ErrorMessage = "ProgramEnrollmentId is required.")]
    public Guid ProgramEnrollmentId { get; set; }

    [Required(ErrorMessage = "ModuleId is required.")]
    public Guid ModuleId { get; set; }
}
