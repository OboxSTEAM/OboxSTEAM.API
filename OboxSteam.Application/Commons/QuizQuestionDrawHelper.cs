using OboxSteam.Application.Validation;
using OboxSteam.Domain.Entities;

namespace OboxSteam.Application.Commons;

/// <summary>
/// Randomly draws bank questions by difficulty tier for Mode A quiz attempts.
/// </summary>
public static class QuizQuestionDrawHelper
{
    public static List<BankQuestion> Draw(
        IReadOnlyList<BankQuestion> allQuestions,
        int drawCount,
        int easyPercent,
        int mediumPercent,
        int hardPercent,
        bool allowShuffle)
    {
        QuizOperationValidator.ValidateDrawInput(allQuestions, drawCount);

        drawCount = Math.Min(drawCount, allQuestions.Count);

        var easyPool = allQuestions.Where(q => q.DifficultyLevel <= 2).ToList();
        var mediumPool = allQuestions.Where(q => q.DifficultyLevel == 3).ToList();
        var hardPool = allQuestions.Where(q => q.DifficultyLevel >= 4).ToList();

        var easyCount = (int)Math.Round(drawCount * easyPercent / 100.0, MidpointRounding.AwayFromZero);
        var mediumCount = (int)Math.Round(drawCount * mediumPercent / 100.0, MidpointRounding.AwayFromZero);
        var hardCount = drawCount - easyCount - mediumCount;

        if (hardCount < 0)
        {
            hardCount = 0;
            var overflow = easyCount + mediumCount - drawCount;
            if (mediumCount >= overflow)
                mediumCount -= overflow;
            else
            {
                overflow -= mediumCount;
                mediumCount = 0;
                easyCount = Math.Max(0, easyCount - overflow);
            }

            hardCount = drawCount - easyCount - mediumCount;
        }

        var picked = new List<BankQuestion>();
        var usedIds = new HashSet<Guid>();

        PickFromPool(picked, usedIds, easyPool, easyCount);
        PickFromPool(picked, usedIds, mediumPool, mediumCount);
        PickFromPool(picked, usedIds, hardPool, hardCount);

        if (picked.Count < drawCount)
        {
            var remaining = allQuestions
                .Where(q => !usedIds.Contains(q.Id))
                .OrderBy(_ => Random.Shared.Next())
                .Take(drawCount - picked.Count);

            foreach (var question in remaining)
            {
                picked.Add(question);
                usedIds.Add(question.Id);
            }
        }

        if (allowShuffle)
            picked = picked.OrderBy(_ => Random.Shared.Next()).ToList();

        return picked;
    }

    private static void PickFromPool(
        List<BankQuestion> picked,
        HashSet<Guid> usedIds,
        List<BankQuestion> pool,
        int count)
    {
        if (count <= 0 || pool.Count == 0)
            return;

        foreach (var question in pool.OrderBy(_ => Random.Shared.Next()).Take(count))
        {
            if (usedIds.Add(question.Id))
                picked.Add(question);
        }
    }
}
