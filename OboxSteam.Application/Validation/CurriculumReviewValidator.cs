using OboxSteam.Application.Utils;

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
}
