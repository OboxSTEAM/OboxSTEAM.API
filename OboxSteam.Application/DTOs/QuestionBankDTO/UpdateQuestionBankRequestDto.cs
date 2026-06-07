namespace OboxSteam.Application.DTOs.QuestionBankDTO;

public class UpdateQuestionBankRequestDto
{
    public Guid? CourseId { get; set; }
    public string? Name { get; set; }
    public string? Description { get; set; }
}
