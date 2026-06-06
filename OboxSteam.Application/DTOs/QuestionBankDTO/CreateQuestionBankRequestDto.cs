namespace OboxSteam.Application.DTOs.QuestionBankDTO;

public class CreateQuestionBankRequestDto
{
    public Guid CourseId { get; set; }
    public string Name { get; set; } = null!;
    public string? Description { get; set; }
}
