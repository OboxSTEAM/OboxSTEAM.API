using System.ComponentModel.DataAnnotations;

namespace OboxSteam.Application.DTOs.ResearchSubmissionDTO;

public class StartResearchSubmissionForClassRequestDto
{
    [Required(ErrorMessage = "ClassId is required.")]
    public Guid ClassId { get; set; }

    [Required(ErrorMessage = "ResearchMilestoneId is required.")]
    public Guid ResearchMilestoneId { get; set; }
}
