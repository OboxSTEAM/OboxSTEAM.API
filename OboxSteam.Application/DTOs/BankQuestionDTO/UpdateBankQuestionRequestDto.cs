namespace OboxSteam.Application.DTOs.BankQuestionDTO;

public class UpdateBankQuestionRequestDto
{
    public Guid? QuestionBankId { get; set; }
    public string? QuestionText { get; set; }
    public string? QuestionType { get; set; }
    public decimal? Points { get; set; }
    public int? DifficultyLevel { get; set; }
    public int? OrderIndex { get; set; }
    public List<UpdateBankQuestionOptionRequestDto>? Options { get; set; }
}
