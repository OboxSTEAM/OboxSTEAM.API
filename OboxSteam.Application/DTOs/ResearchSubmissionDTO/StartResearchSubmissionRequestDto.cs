using System.ComponentModel.DataAnnotations;

namespace OboxSteam.Application.DTOs.ResearchSubmissionDTO;

public class StartResearchSubmissionRequestDto
{
    [Required(ErrorMessage = "ModuleEnrollmentId is required.")]
    public Guid ModuleEnrollmentId { get; set; }

    [Required(ErrorMessage = "ResearchMilestoneId is required.")]
    public Guid ResearchMilestoneId { get; set; }
}
