using System.ComponentModel.DataAnnotations;

namespace OboxSteam.Application.DTOs.AssignmentSubmissionDTO;

public class GradeAssignmentSubmissionRequestDto
{
    [Required(ErrorMessage = "AssignedGrade is required.")]
    public decimal AssignedGrade { get; set; }

    public string? MentorFeedback { get; set; }

    /// <summary>
    /// When true, the submission moves to <c>ReturnedForRevision</c> instead of <c>Graded</c>.
    /// </summary>
    public bool ReturnForRevision { get; set; }
}
