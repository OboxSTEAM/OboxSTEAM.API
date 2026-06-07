namespace OboxSteam.Application.Interfaces;

public interface IBankQuestionService
{
    Task<bool> DeleteBankQuestion(Guid questionBankId, Guid questionId);
}
