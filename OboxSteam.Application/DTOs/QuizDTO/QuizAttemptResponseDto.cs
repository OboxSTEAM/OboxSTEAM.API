namespace OboxSteam.Application.DTOs.QuizDTO;

/// <summary>
/// Quiz attempt payload for start and resume (GET). Mode A bank-based snapshot.
/// Time limit comes from the assignment; per-attempt timer uses <see cref="StartedAt"/> and <see cref="ExpiresAt"/>.
/// </summary>
public class QuizAttemptResponseDto
{
    public Guid SubmissionId { get; set; }
    public Guid AssignmentId { get; set; }
    public Guid StudentId { get; set; }
    public string? StudentName { get; set; }
    public int AttemptNumber { get; set; }
    public int? TimeLimitMinutes { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public IReadOnlyList<QuizQuestionForStudentDto> Questions { get; set; } = [];
    public IReadOnlyList<QuizAnswerItemDto> SavedAnswers { get; set; } = [];
}
