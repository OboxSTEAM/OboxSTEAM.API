using System.ComponentModel.DataAnnotations;

namespace OboxSteam.Application.DTOs.ActivityProgressDTO;

/// <summary>
/// Request to mark an activity as done within a module enrollment.
/// </summary>
public class UpdateActivityProgressRequestDto
{
    [Required(ErrorMessage = "ModuleEnrollmentId is required.")]
    public Guid ModuleEnrollmentId { get; set; }

    [Required(ErrorMessage = "ActivityId is required.")]
    public Guid ActivityId { get; set; }
}
