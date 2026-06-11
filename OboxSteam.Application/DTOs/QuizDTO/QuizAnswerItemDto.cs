namespace OboxSteam.Application.DTOs.QuizDTO;

/// <summary>
/// One question's selected options. SingleChoice → one id; MultipleChoice → many ids.
/// </summary>
public class QuizAnswerItemDto
{
    public Guid QuestionId { get; set; }
    public IReadOnlyList<Guid> SelectedOptionIds { get; set; } = [];
}
