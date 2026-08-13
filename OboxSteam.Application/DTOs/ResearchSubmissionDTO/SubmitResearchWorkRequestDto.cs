using System.ComponentModel.DataAnnotations;

namespace OboxSteam.Application.DTOs.ResearchSubmissionDTO;

/// <summary>
/// Student request to turn in research work for a milestone.
/// At least one of <see cref="ContentText"/>, <see cref="FileUrl"/>, or <see cref="EvidenceUrls"/> is required.
/// </summary>
public class SubmitResearchWorkRequestDto
{
    [Required(ErrorMessage = "ModuleEnrollmentId is required.")]
    public Guid ModuleEnrollmentId { get; set; }

    [Required(ErrorMessage = "ResearchMilestoneId is required.")]
    public Guid ResearchMilestoneId { get; set; }

    public string? ContentText { get; set; }

    public string? FileUrl { get; set; }

    public List<string>? EvidenceUrls { get; set; }
}
