namespace OboxSteam.Application.DTOs.QuizDTO;

public class SubmitQuizAnswersRequestDto
{
    public IReadOnlyList<QuizAnswerItemDto> Answers { get; set; } = [];
}
