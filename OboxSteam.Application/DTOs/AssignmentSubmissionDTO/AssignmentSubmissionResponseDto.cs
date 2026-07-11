using OboxSteam.Domain.Enums;

namespace OboxSteam.Application.DTOs.AssignmentSubmissionDTO;

public class AssignmentSubmissionResponseDto
{
    public Guid Id { get; set; }

    public string Code { get; set; } = null!;

    public Guid AssignmentId { get; set; }

    public AssignmentType AssignmentType { get; set; }

    public Guid? ModuleEnrollmentId { get; set; }

    public Guid StudentId { get; set; }

    public int AttemptNumber { get; set; }

    public SubmissionStatus Status { get; set; }

    public string? ContentText { get; set; }

    public string? FileUrl { get; set; }

    public decimal? AssignedGrade { get; set; }

    public decimal PassScore { get; set; }

    public int MaxPoints { get; set; }

    /// <summary>Null until graded; otherwise whether the grade met the pass score.</summary>
    public bool? Passed { get; set; }

    public string? MentorFeedback { get; set; }

    public Guid? VerifiedBy { get; set; }

    public DateTime? SubmittedAt { get; set; }

    public DateTime? GradedAt { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }
}
