namespace OboxSteam.Application.DTOs.BankQuestionDTO;

public class ImportBankQuestionsResultDto
{
    public int TotalRows { get; set; }
    public int ImportedCount { get; set; }
    public int FailedCount { get; set; }
    public List<ImportRowErrorDto> Errors { get; set; } = new();
    public List<BankQuestionResponseDto> ImportedQuestions { get; set; } = new();
}
