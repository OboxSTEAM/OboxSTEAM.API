using System.ComponentModel.DataAnnotations;

namespace OboxSteam.Application.DTOs.ResearchSubmissionDTO;

public class GradeResearchSubmissionRequestDto
{
    [Required(ErrorMessage = "AssignedGrade is required.")]
    public decimal AssignedGrade { get; set; }

    public string? MentorFeedback { get; set; }

    /// <summary>
    /// When true, submission moves to <c>ReturnedForRevision</c> instead of <c>Graded</c>.
    /// </summary>
    public bool ReturnForRevision { get; set; }
}
