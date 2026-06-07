namespace OboxSteam.Application.DTOs.BankQuestionDTO;

public class BankQuestionResponseDto
{
    public Guid Id { get; set; }
    public Guid QuestionBankId { get; set; }
    public string QuestionText { get; set; } = null!;
    public string QuestionType { get; set; } = null!;
    public decimal Points { get; set; }
    public int DifficultyLevel { get; set; }
    public int OrderIndex { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public List<BankQuestionOptionResponseDto> Options { get; set; } = new();
}
