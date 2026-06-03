using System.ComponentModel.DataAnnotations;

namespace OboxSteam.Application.DTOs.EnrollmentDTO;

/// <summary>
/// Request to enroll in a program.
/// Business rule: student must have completed payment for this program before enrollment is allowed.
/// Payment validation will be enforced in the service layer when payment is implemented.
/// </summary>
public class CreateEnrollmentProgramRequestDto
{
    [Required(ErrorMessage = "ProgramId is required.")]
    public Guid ProgramId { get; set; }
}
