namespace OboxSteam.Application.DTOs.ClassQuizQuestionSetDTO;

public class UpdateClassQuizQuestionRequestDto
{
    public string? QuestionText { get; set; }
    public string? QuestionType { get; set; }
    public decimal? Points { get; set; }
    public int? DifficultyLevel { get; set; }
    public int? OrderIndex { get; set; }
    public List<UpdateClassQuizQuestionOptionRequestDto>? Options { get; set; }
}
