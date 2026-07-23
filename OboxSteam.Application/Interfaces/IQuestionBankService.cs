using Microsoft.AspNetCore.Http;
using OboxSteam.Application.Commons;
using OboxSteam.Application.DTOs.BankQuestionDTO;
using OboxSteam.Application.DTOs.QuestionBankDTO;

namespace OboxSteam.Application.Interfaces;

public interface IQuestionBankService
{
    /// <summary>
    /// Get a paginated list of question banks with program/module/course context.
    /// Supports search (bank, course, program, module name), filter, and sort.
    /// </summary>
    Task<Pagination<QuestionBankListItemDto>> GetAllQuestionBanks(
        string? search,
        string? sortBy,
        bool isDescending,
        int page,
        int pageSize,
        Guid? courseId = null,
        Guid? programId = null,
        Guid? moduleId = null);

    Task<QuestionBankResponseDto> CreateQuestionBank(CreateQuestionBankRequestDto request);
    Task<QuestionBankResponseDto?> GetQuestionBankById(Guid questionBankId);
    Task<bool> DeleteQuestionBank(Guid questionBankId);
    Task<ImportBankQuestionsResultDto> ImportQuestionsFromCsv(Guid questionBankId, IFormFile file);
}
