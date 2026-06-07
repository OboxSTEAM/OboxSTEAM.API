namespace OboxSteam.Application.DTOs.BankQuestionDTO;

public class ImportRowErrorDto
{
    public int RowNumber { get; set; }
    public string QuestionText { get; set; } = string.Empty;
    public string Error { get; set; } = null!;
}
