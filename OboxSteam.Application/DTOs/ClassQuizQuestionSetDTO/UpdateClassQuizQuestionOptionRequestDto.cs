namespace OboxSteam.Application.DTOs.ClassQuizQuestionSetDTO;

public class UpdateClassQuizQuestionOptionRequestDto
{
    public Guid? Id { get; set; }
    public string? OptionText { get; set; }
    public bool? IsCorrect { get; set; }
}
