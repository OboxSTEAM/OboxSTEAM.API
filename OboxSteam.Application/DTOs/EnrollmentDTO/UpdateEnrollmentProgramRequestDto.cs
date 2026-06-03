using System.ComponentModel.DataAnnotations;

namespace OboxSteam.Application.DTOs.EnrollmentDTO;

/// <summary>
/// Request to re-enroll in a program after failing or not passing.
/// Business rule: student must have completed the program retake payment before re-enrollment is allowed.
/// Payment validation will be enforced in the service layer when payment is implemented.
/// </summary>
public class UpdateEnrollmentProgramRequestDto
{
    [Required(ErrorMessage = "ProgramId is required.")]
    public Guid ProgramId { get; set; }
}
