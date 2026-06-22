using System.ComponentModel.DataAnnotations;

namespace OboxSteam.Application.DTOs.ActivityProgressDTO;

/// <summary>
/// Request to start tracking progress for an activity within a module enrollment.
/// </summary>
public class CreateActivityProgressRequestDto
{
    [Required(ErrorMessage = "ModuleEnrollmentId is required.")]
    public Guid ModuleEnrollmentId { get; set; }

    [Required(ErrorMessage = "ActivityId is required.")]
    public Guid ActivityId { get; set; }
}
