using OboxSteam.Application.DTOs.BankQuestionDTO;

namespace OboxSteam.Application.Interfaces;

public interface ICsvQuestionParserService
{
    Task<IReadOnlyList<CsvBankQuestionRowDto>> ParseAsync(
        Stream csvStream,
        CancellationToken cancellationToken = default);
}
