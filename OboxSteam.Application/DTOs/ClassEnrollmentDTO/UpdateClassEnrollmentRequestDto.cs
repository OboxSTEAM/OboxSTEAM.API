using System.ComponentModel.DataAnnotations;

namespace OboxSteam.Application.DTOs.ClassEnrollmentDTO;

/// <summary>
/// Request to transfer from the current class to another cohort.
/// Business rules enforced in the service layer:
/// the target class must belong to the same program as the existing enrollment;
/// the target class must differ from the current class;
/// late-join guard via <c>Class.MinHoursBeforeAssignmentJoin</c> (managers may bypass).
/// </summary>
public class UpdateClassEnrollmentRequestDto
{
    [Required(ErrorMessage = "ClassId is required.")]
    public Guid ClassId { get; set; }
}
