using System.ComponentModel.DataAnnotations;

namespace OboxSteam.Application.DTOs.ActivityProgressDTO;

/// <summary>
/// Test-only request to force an activity to Done for a student, bypassing all
/// business rules (enrollment status, ownership, activity type, sequential lock,
/// schedule windows). The module enrollment is resolved automatically from the
/// activity's module and the student's latest attempt.
/// </summary>
public class ForceCompleteActivityRequestDto
{
    [Required(ErrorMessage = "StudentId is required.")]
    public Guid StudentId { get; set; }

    [Required(ErrorMessage = "ActivityId is required.")]
    public Guid ActivityId { get; set; }
}
