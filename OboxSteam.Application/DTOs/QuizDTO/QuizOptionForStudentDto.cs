namespace OboxSteam.Application.DTOs.QuizDTO;

/// <summary>
/// Answer option exposed to students. Does not include <c>IsCorrect</c>.
/// </summary>
public class QuizOptionForStudentDto
{
    public Guid Id { get; set; }
    public string OptionText { get; set; } = null!;
}
