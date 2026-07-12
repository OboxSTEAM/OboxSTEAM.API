using System.ComponentModel.DataAnnotations;

namespace OboxSteam.Application.DTOs.AssignmentSubmissionDTO;

/// <summary>
/// Student request to turn in a FileUpload assignment.
/// At least one of <see cref="ContentText"/> or <see cref="FileUrl"/> must be provided.
/// </summary>
public class SubmitAssignmentRequestDto
{
    [Required(ErrorMessage = "AssignmentId is required.")]
    public Guid AssignmentId { get; set; }

    public string? ContentText { get; set; }

    public string? FileUrl { get; set; }
}
