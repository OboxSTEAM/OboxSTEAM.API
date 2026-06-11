using OboxSteam.Application.Validation;
using OboxSteam.Domain.Entities;

namespace OboxSteam.Application.Commons;

public sealed class QuizGradeResult
{
    public decimal AssignedGrade { get; init; }
    public int CorrectCount { get; init; }
    public int TotalQuestions { get; init; }
    public bool Passed { get; init; }
}

/// <summary>
/// Auto-grades quiz snapshots using equal points per question: MaxPoints / questionCount.
/// </summary>
public static class QuizScoreCalculator
{
    public static QuizGradeResult Calculate(
        Assignment assignment,
        IReadOnlyList<QuizQuestion> snapshotQuestions,
        IReadOnlyList<QuizAnswer> answers)
    {
        QuizOperationValidator.ValidateGradingInput(assignment, snapshotQuestions);

        var totalQuestions = snapshotQuestions.Count;
        var pointsPerQuestion = assignment.MaxPoints / (decimal)totalQuestions;
        var answersByQuestion = answers
            .GroupBy(a => a.QuizQuestionId)
            .ToDictionary(g => g.Key, g => g.Select(a => a.QuizOptionId).ToHashSet());

        var correctCount = 0;

        foreach (var question in snapshotQuestions)
        {
            answersByQuestion.TryGetValue(question.Id, out var selectedIds);
            selectedIds ??= [];

            if (IsQuestionCorrect(question, selectedIds))
                correctCount++;
        }

        var assignedGrade = Math.Round(correctCount * pointsPerQuestion, 2, MidpointRounding.AwayFromZero);
        var passed = assignedGrade >= assignment.PassScore;

        return new QuizGradeResult
        {
            AssignedGrade = assignedGrade,
            CorrectCount = correctCount,
            TotalQuestions = totalQuestions,
            Passed = passed
        };
    }

    public static bool IsQuestionCorrect(QuizQuestion question, IReadOnlySet<Guid> selectedOptionIds)
    {
        var activeOptions = question.Options.Where(o => !o.IsDeleted).ToList();
        var correctIds = activeOptions.Where(o => o.IsCorrect).Select(o => o.Id).ToHashSet();

        if (string.Equals(question.QuestionType, QuestionTypeConstants.SingleChoice, StringComparison.Ordinal))
        {
            return selectedOptionIds.Count == 1
                   && correctIds.Count == 1
                   && correctIds.SetEquals(selectedOptionIds);
        }

        if (string.Equals(question.QuestionType, QuestionTypeConstants.MultipleChoice, StringComparison.Ordinal))
            return correctIds.SetEquals(selectedOptionIds);

        return false;
    }
}
