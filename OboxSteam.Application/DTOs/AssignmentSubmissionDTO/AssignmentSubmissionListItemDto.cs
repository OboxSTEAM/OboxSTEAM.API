using OboxSteam.Domain.Enums;

namespace OboxSteam.Application.DTOs.AssignmentSubmissionDTO;

/// <summary>
/// Lightweight submission row for the per-class grading board
/// (GET /api/assignments/{assignmentId}/submissions).
/// </summary>
public class AssignmentSubmissionListItemDto
{
    public Guid SubmissionId { get; set; }
    public Guid StudentId { get; set; }
    public string? StudentName { get; set; }
    public int AttemptNumber { get; set; }
    public SubmissionStatus Status { get; set; }
    public decimal? AssignedGrade { get; set; }

    /// <summary>Null until the submission is graded with a numeric grade.</summary>
    public bool? Passed { get; set; }
    public DateTime? SubmittedAt { get; set; }
    public DateTime? GradedAt { get; set; }
}
