namespace OboxSteam.Application.DTOs.BankQuestionDTO;

public class UpdateBankQuestionOptionRequestDto
{
    public Guid? Id { get; set; }
    public string? OptionText { get; set; }
    public bool? IsCorrect { get; set; }
}
