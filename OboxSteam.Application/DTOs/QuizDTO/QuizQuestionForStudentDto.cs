namespace OboxSteam.Application.DTOs.QuizDTO;

/// <summary>
/// Quiz question snapshot exposed to students (Mode A: per-submission snapshot).
/// </summary>
public class QuizQuestionForStudentDto
{
    public Guid Id { get; set; }
    public string QuestionText { get; set; } = null!;
    public string QuestionType { get; set; } = null!;
    public decimal Points { get; set; }
    public int OrderIndex { get; set; }
    public IReadOnlyList<QuizOptionForStudentDto> Options { get; set; } = [];
}
