using Microsoft.AspNetCore.Http;
using OboxSteam.Application.DTOs.BankQuestionDTO;
using OboxSteam.Application.DTOs.QuestionBankDTO;

namespace OboxSteam.Application.Interfaces;

public interface IQuestionBankService
{
    Task<QuestionBankResponseDto> CreateQuestionBank(CreateQuestionBankRequestDto request);
    Task<QuestionBankResponseDto?> GetQuestionBankById(Guid questionBankId);
    Task<bool> DeleteQuestionBank(Guid questionBankId);
    Task<ImportBankQuestionsResultDto> ImportQuestionsFromCsv(Guid questionBankId, IFormFile file);
}
