using System.ComponentModel.DataAnnotations;

namespace OboxSteam.Application.DTOs.ClassEnrollmentDTO;

/// <summary>
/// Request to join a class cohort within an active program enrollment.
/// Business rules enforced in the service layer:
/// the selected class must belong to the same program as the program enrollment;
/// one active class enrollment per program enrollment in v1;
/// late-join guard via <c>Class.MinHoursBeforeAssignmentJoin</c> (managers may bypass).
/// </summary>
public class CreateClassEnrollmentRequestDto
{
    [Required(ErrorMessage = "ProgramEnrollmentId is required.")]
    public Guid ProgramEnrollmentId { get; set; }

    [Required(ErrorMessage = "ClassId is required.")]
    public Guid ClassId { get; set; }
}
