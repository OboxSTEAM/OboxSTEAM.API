namespace OboxSteam.Application.DTOs.ClassQuizQuestionSetDTO;

public class ClassQuizQuestionResponseDto
{
    public Guid Id { get; set; }
    public Guid? SourceBankQuestionId { get; set; }
    public string QuestionText { get; set; } = null!;
    public string QuestionType { get; set; } = null!;
    public decimal Points { get; set; }
    public int DifficultyLevel { get; set; }
    public int OrderIndex { get; set; }
    public List<ClassQuizQuestionOptionResponseDto> Options { get; set; } = new();
}
