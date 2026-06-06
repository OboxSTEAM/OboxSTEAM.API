namespace OboxSteam.Application.DTOs.BankQuestionDTO;

public class BankQuestionOptionResponseDto
{
    public Guid Id { get; set; }
    public Guid BankQuestionId { get; set; }
    public string OptionText { get; set; } = null!;
    public bool IsCorrect { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
