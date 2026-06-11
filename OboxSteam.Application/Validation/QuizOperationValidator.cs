using OboxSteam.Application.Utils;
using OboxSteam.Domain.Entities;

namespace OboxSteam.Application.Validation;

/// <summary>
/// Pre-condition rules for quiz draw and grading operations (Mode A).
/// </summary>
public static class QuizOperationValidator
{
    public static void ValidateDrawInput(IReadOnlyList<BankQuestion> allQuestions, int drawCount)
    {
        if (allQuestions.Count == 0)
            throw ErrorHelper.BadRequest("Question bank has no questions.");

        if (drawCount <= 0)
            throw ErrorHelper.BadRequest("Draw count must be greater than 0.");
    }

    public static void ValidateGradingInput(
        Assignment assignment,
        IReadOnlyList<QuizQuestion> snapshotQuestions)
    {
        if (assignment.MaxPoints <= 0)
            throw ErrorHelper.BadRequest("Assignment MaxPoints must be greater than 0.");

        if (snapshotQuestions.Count == 0)
            throw ErrorHelper.BadRequest("Cannot grade a quiz with no questions.");
    }
}
