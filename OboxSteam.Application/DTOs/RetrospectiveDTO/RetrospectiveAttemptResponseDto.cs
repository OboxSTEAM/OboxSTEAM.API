using OboxSteam.Domain.Enums;

namespace OboxSteam.Application.DTOs.RetrospectiveDTO;

public class RetrospectiveAttemptResponseDto
{
    public Guid SubmissionId { get; set; }

    public Guid AssignmentId { get; set; }

    public string Title { get; set; } = null!;

    public string? Description { get; set; }

    public int AttemptNumber { get; set; }

    public SubmissionStatus Status { get; set; }

    /// <summary>Plain-text draft or submitted reflection.</summary>
    public string? ContentText { get; set; }

    public DateTime? LastSavedAt { get; set; }

    public decimal? AssignedGrade { get; set; }

    public decimal PassScore { get; set; }

    public int MaxPoints { get; set; }

    public bool? Passed { get; set; }

    public string? MentorFeedback { get; set; }

    public DateTime? SubmittedAt { get; set; }

    public DateTime? GradedAt { get; set; }
}
