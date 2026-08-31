using OboxSteam.Application.DTOs.CurriculumReviewDTO;
using OboxSteam.Application.Utils;
using OboxSteam.Domain.Entities;

namespace OboxSteam.Application.Validation;

public static class CurriculumReviewValidator
{
    public const int MaxCommentLength = 4000;

    public static string RequireComment(string? comment)
    {
        var normalized = NormalizeOptionalComment(comment);
        if (normalized == null)
        {
            throw ErrorHelper.BadRequest("A comment is required when requesting curriculum changes.");
        }

        return normalized;
    }

    public static string? NormalizeOptionalComment(string? comment)
    {
        if (string.IsNullOrWhiteSpace(comment))
        {
            return null;
        }

        var trimmed = comment.Trim();
        if (trimmed.Length > MaxCommentLength)
        {
            throw ErrorHelper.BadRequest($"Comment must be at most {MaxCommentLength} characters.");
        }

        return trimmed;
    }

    public static IReadOnlyList<ReviewCriterionScore> BuildScores(
        Guid reviewId,
        IReadOnlyList<FrameworkRubricCriterion> criteria,
        IReadOnlyList<ReviewCriterionScoreRequest>? scores)
    {
        if (criteria.Count == 0)
        {
            if (scores is { Count: > 0 })
            {
                throw ErrorHelper.BadRequest("This framework has no rubric criteria; scores cannot be submitted.");
            }

            return [];
        }

        if (scores == null || scores.Count == 0)
        {
            throw ErrorHelper.BadRequest("A score is required for every framework rubric criterion.");
        }

        var criteriaById = criteria.ToDictionary(c => c.Id);
        var seen = new HashSet<Guid>();
        var rows = new List<ReviewCriterionScore>();

        foreach (var item in scores)
        {
            if (item.CriterionId == Guid.Empty)
            {
                throw ErrorHelper.BadRequest("Criterion id is required.");
            }

            if (!seen.Add(item.CriterionId))
            {
                throw ErrorHelper.BadRequest($"Duplicate score for criterion '{item.CriterionId}'.");
            }

            if (!criteriaById.TryGetValue(item.CriterionId, out var criterion))
            {
                throw ErrorHelper.BadRequest($"Criterion '{item.CriterionId}' is not on this framework.");
            }

            if (item.Score < 0 || item.Score > criterion.MaxScore)
            {
                throw ErrorHelper.BadRequest(
                    $"Score for '{criterion.Name}' must be between 0 and {criterion.MaxScore}.");
            }

            rows.Add(new ReviewCriterionScore
            {
                CurriculumReviewId = reviewId,
                FrameworkRubricCriterionId = criterion.Id,
                Score = item.Score,
                Comment = NormalizeOptionalComment(item.Comment),
            });
        }

        if (rows.Count != criteria.Count)
        {
            throw ErrorHelper.BadRequest("A score is required for every framework rubric criterion.");
        }

        return rows;
    }
}
