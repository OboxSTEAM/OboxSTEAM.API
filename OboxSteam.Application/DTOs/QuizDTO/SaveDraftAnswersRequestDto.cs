namespace OboxSteam.Application.DTOs.QuizDTO;

public class SaveDraftAnswersRequestDto
{
    public IReadOnlyList<QuizAnswerItemDto> Answers { get; set; } = [];
}
