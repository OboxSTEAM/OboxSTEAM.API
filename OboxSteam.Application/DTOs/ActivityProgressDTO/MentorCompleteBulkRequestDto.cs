using System.ComponentModel.DataAnnotations;

namespace OboxSteam.Application.DTOs.ActivityProgressDTO;

/// <summary>
/// Mentor/Manager request to mark a LiveOnline/Offline session activity Done
/// for every active student on the class roster after attendance has been taken.
/// </summary>
public class MentorCompleteBulkRequestDto
{
    [Required(ErrorMessage = "ClassSessionId is required.")]
    public Guid ClassSessionId { get; set; }

    [Required(ErrorMessage = "ActivityId is required.")]
    public Guid ActivityId { get; set; }
}
