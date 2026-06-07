namespace OboxSteam.Application.Commons;

public static class BankQuestionValidationHelper
{
    public static string? ValidateQuestionRules(
        string normalizedQuestionType,
        IReadOnlyList<(string OptionText, bool IsCorrect)> options)
    {
        if (options.Count < 2)
            return "At least 2 options are required.";

        var correctCount = options.Count(o => o.IsCorrect);

        if (normalizedQuestionType == QuestionTypeConstants.SingleChoice && correctCount != 1)
            return "Single choice questions must have exactly 1 correct option.";

        if (normalizedQuestionType == QuestionTypeConstants.MultipleChoice && correctCount < 1)
            return "Multiple choice questions must have at least 1 correct option.";

        return null;
    }
}
