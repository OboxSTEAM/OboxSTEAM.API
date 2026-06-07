using Microsoft.Extensions.Logging;
using OboxSteam.Application.Interfaces;
using OboxSteam.Domain.Interfaces;

namespace OboxSteam.Application.Services;

public sealed class BankQuestionService : IBankQuestionService
{
    private readonly IClaimsService _claimsService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<BankQuestionService> _logger;

    public BankQuestionService(
        IClaimsService claimsService,
        IUnitOfWork unitOfWork,
        ILogger<BankQuestionService> logger)
    {
        _claimsService = claimsService;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<bool> DeleteBankQuestion(Guid questionBankId, Guid questionId)
    {
        var userId = _claimsService.GetCurrentUserId;
        _logger.LogInformation(
            "DeleteBankQuestion started by UserId={UserId} for BankQuestionId={BankQuestionId}",
            userId, questionId);

        var question = await _unitOfWork.BankQuestions.GetByIdAsync(questionId, q => q.Options);
        if (question == null || question.IsDeleted || question.QuestionBankId != questionBankId)
            return false;

        await _unitOfWork.BankQuestions.SoftRemove(question);

        if (question.Options.Count > 0)
            await _unitOfWork.BankQuestionOptions.SoftRemoveRange(
                question.Options.Where(o => !o.IsDeleted).ToList());

        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation(
            "DeleteBankQuestion completed. BankQuestionId={BankQuestionId}",
            questionId);

        return true;
    }
}
