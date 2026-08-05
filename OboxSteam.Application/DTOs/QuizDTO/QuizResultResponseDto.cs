using OboxSteam.Domain.Enums;

namespace OboxSteam.Application.DTOs.QuizDTO;

public class QuizResultResponseDto
{
    public Guid SubmissionId { get; set; }
    public Guid AssignmentId { get; set; }
    public Guid StudentId { get; set; }
    public string? StudentName { get; set; }
    public int AttemptNumber { get; set; }
    public DateTime? StartedAt { get; set; }
    public decimal AssignedGrade { get; set; }
    public int MaxPoints { get; set; }
    public decimal PassScore { get; set; }
    public bool Passed { get; set; }
    public int CorrectCount { get; set; }
    public int TotalQuestions { get; set; }
    public SubmissionStatus Status { get; set; }
    public DateTime? SubmittedAt { get; set; }
}
