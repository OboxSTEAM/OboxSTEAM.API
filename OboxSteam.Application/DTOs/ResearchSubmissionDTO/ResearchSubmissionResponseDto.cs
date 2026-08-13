using OboxSteam.Domain.Enums;

namespace OboxSteam.Application.DTOs.ResearchSubmissionDTO;

public class ResearchSubmissionResponseDto
{
    public Guid Id { get; set; }
    public string Code { get; set; } = null!;
    public Guid AssignmentId { get; set; }
    public Guid ResearchMilestoneId { get; set; }
    public Guid? ModuleEnrollmentId { get; set; }
    public Guid StudentId { get; set; }
    public int AttemptNumber { get; set; }
    public SubmissionStatus Status { get; set; }
    public string? ContentText { get; set; }
    public string? FileUrl { get; set; }

    /// <summary>Preview URLs for linked evidence media (may be empty while video is still transcoding).</summary>
    public List<string> EvidenceUrls { get; set; } = [];

    /// <summary>
    /// MediaAsset ids linked as evidence. Echo these on resubmit as
    /// <c>EvidenceMediaAssetIds</c> when the student keeps existing evidence.
    /// </summary>
    public List<Guid> EvidenceMediaAssetIds { get; set; } = [];
    public decimal? AssignedGrade { get; set; }
    public decimal PassScore { get; set; }
    public int MaxPoints { get; set; }
    public bool? Passed { get; set; }
    public string? MentorFeedback { get; set; }
    public Guid? VerifiedBy { get; set; }
    public DateTime? SubmittedAt { get; set; }
    public DateTime? GradedAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
