namespace OboxSteam.Application.DTOs.ClassQuizQuestionSetDTO;

public class ClassQuizQuestionOptionResponseDto
{
    public Guid Id { get; set; }
    public string OptionText { get; set; } = null!;
    public bool IsCorrect { get; set; }
}
