using System.ComponentModel.DataAnnotations;

namespace OboxSteam.Application.DTOs.ClassEnrollmentDTO;

/// <summary>
/// Manager request to transfer a student: marks the current active class enrollment
/// as Transferred and creates a new Active enrollment in another Open cohort
/// within the same program. Student is identified by the route <c>id</c>.
/// </summary>
public class ManagerTransferClassRequestDto
{
    [Required(ErrorMessage = "ClassId is required.")]
    public Guid ClassId { get; set; }
}
