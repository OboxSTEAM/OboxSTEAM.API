using System.ComponentModel.DataAnnotations;

namespace OboxSteam.Application.DTOs.ResearchSubmissionDTO;

/// <summary>
/// Student request to turn in research work for a milestone.
/// At least one of <see cref="ContentText"/>, <see cref="FileUrl"/>, or
/// <see cref="EvidenceMediaAssetIds"/> is required.
/// </summary>
public class SubmitResearchWorkRequestDto
{
    [Required(ErrorMessage = "ModuleEnrollmentId is required.")]
    public Guid ModuleEnrollmentId { get; set; }

    [Required(ErrorMessage = "ResearchMilestoneId is required.")]
    public Guid ResearchMilestoneId { get; set; }

    public string? ContentText { get; set; }

    public string? FileUrl { get; set; }

    /// <summary>
    /// MediaAsset ids returned from evidence upload (<c>isEvidence=true</c>).
    /// Must already exist from the media AI pipeline — not created from URLs here.
    /// </summary>
    public List<Guid>? EvidenceMediaAssetIds { get; set; }
}
