namespace OboxSteam.Application.DTOs.BankQuestionDTO;

public class CsvBankQuestionRowDto
{
    public int RowNumber { get; set; }
    public string QuestionText { get; set; } = null!;
    public string QuestionType { get; set; } = null!;
    public string Difficulty { get; set; } = null!;
    public decimal Points { get; set; }
    public List<CsvBankQuestionOptionRowDto> Options { get; set; } = new();
    public List<string> ParseErrors { get; set; } = new();
}
