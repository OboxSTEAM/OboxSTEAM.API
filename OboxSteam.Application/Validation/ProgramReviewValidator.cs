using System.Text.RegularExpressions;
using OboxSteam.Application.Utils;
using OboxSteam.Domain.Entities;
using OboxSteam.Domain.Enums;
using OboxSteam.Domain.Interfaces;

namespace OboxSteam.Application.Validation;

/// <summary>
/// Shared rules for student program reviews: who may create a review and how input is normalized.
/// Create (POST) and the "my review" lookup both go through <see cref="ResolveEligibilityAsync"/>
/// so the two cannot drift apart.
/// </summary>
public static class ProgramReviewValidator
{
    public const int COMMENT_MAX_LENGTH = 2000;
    public const string REVIEW_NOT_ELIGIBLE = "REVIEW_NOT_ELIGIBLE";
    public const string REVIEW_ALREADY_EXISTS = "REVIEW_ALREADY_EXISTS";
    public const string REVIEW_REMOVED_BY_MODERATOR = "REVIEW_REMOVED_BY_MODERATOR";
    public const string REVIEW_COMMENT_INVALID = "REVIEW_COMMENT_INVALID";

    private static readonly Regex HtmlMarkupPattern = new(
        @"</?[a-zA-Z][a-zA-Z0-9-]*(\s[^<>]*)?/?>|<!--",
        RegexOptions.Compiled);

    public static void ValidateStarRating(int starRating)
    {
        if (starRating < 1 || starRating > 5)
        {
            throw ErrorHelper.BadRequest("StarRating must be between 1 and 5.");
        }
    }

    /// <summary>
    /// Trims the comment and returns null when it is empty. Rejects HTML markup and
    /// comments longer than <see cref="COMMENT_MAX_LENGTH"/> characters after trimming.
    /// </summary>
    public static string? NormalizeComment(string? comment)
    {
        if (comment == null)
        {
            return null;
        }

        var trimmed = comment.Trim();
        if (trimmed.Length == 0)
        {
            return null;
        }

        if (trimmed.Length > COMMENT_MAX_LENGTH)
        {
            throw ErrorHelper.BadRequest(
                $"Comment must be at most {COMMENT_MAX_LENGTH} characters.",
                REVIEW_COMMENT_INVALID);
        }

        if (HtmlMarkupPattern.IsMatch(trimmed))
        {
            throw ErrorHelper.BadRequest(
                "Comment must be plain text; HTML is not allowed.",
                REVIEW_COMMENT_INVALID);
        }

        return trimmed;
    }

    /// <summary>
    /// Resolves whether the student may create a review for the program.
    /// An existing active review wins so the student always sees their own review.
    /// Otherwise the student needs a Completed, non-superseded enrollment, and must not have
    /// had their latest review removed by a moderator (Admin/Manager).
    /// </summary>
    public static async Task<ProgramReviewEligibility> ResolveEligibilityAsync(
        IUnitOfWork unitOfWork,
        Guid programId,
        Guid studentId)
    {
        var activeReview = await unitOfWork.ProgramReviews.FirstOrDefaultAsync(
            r => r.ProgramId == programId && r.StudentId == studentId && !r.IsDeleted);
        if (activeReview != null)
        {
            return new ProgramReviewEligibility(ProgramReviewEligibilityReason.AlreadyReviewed, activeReview);
        }

        var enrollments = await unitOfWork.ProgramEnrollments.GetAllAsync(
            pe => pe.ProgramId == programId
                  && pe.StudentId == studentId
                  && pe.Status != EnrollmentStatus.PendingPayment
                  && !pe.IsDeleted);
        if (enrollments.Count == 0)
        {
            return new ProgramReviewEligibility(ProgramReviewEligibilityReason.NotEnrolled, null);
        }

        var hasCompleted = enrollments.Any(
            pe => pe.Status == EnrollmentStatus.Completed && pe.SupersededByEnrollmentId == null);
        if (!hasCompleted)
        {
            return new ProgramReviewEligibility(ProgramReviewEligibilityReason.NotCompleted, null);
        }

        var deletedReviews = await unitOfWork.ProgramReviews.GetAllIncludingDeletedAsync(
            r => r.ProgramId == programId && r.StudentId == studentId && r.IsDeleted);
        var latestDeleted = deletedReviews
            .OrderByDescending(r => r.DeletedAt ?? r.UpdatedAt ?? r.CreatedAt)
            .FirstOrDefault();
        if (latestDeleted != null && IsRemovedByModerator(latestDeleted))
        {
            return new ProgramReviewEligibility(ProgramReviewEligibilityReason.RemovedByModerator, null);
        }

        return new ProgramReviewEligibility(null, null);
    }

    /// <summary>
    /// Only the owner or an Admin/Manager can delete a review, so any deleter other than
    /// the owner is a moderator.
    /// </summary>
    public static bool IsRemovedByModerator(ProgramReview review)
        => review.IsDeleted
           && review.DeletedBy is Guid deletedBy
           && deletedBy != Guid.Empty
           && deletedBy != review.StudentId;

    public static void EnsureCanCreate(ProgramReviewEligibility eligibility)
    {
        switch (eligibility.Reason)
        {
            case null:
                return;
            case ProgramReviewEligibilityReason.AlreadyReviewed:
                throw ErrorHelper.Conflict(
                    "You have already submitted a review for this program.",
                    REVIEW_ALREADY_EXISTS);
            case ProgramReviewEligibilityReason.RemovedByModerator:
                throw ErrorHelper.Forbidden(
                    "Your previous review for this program was removed by a moderator.",
                    REVIEW_REMOVED_BY_MODERATOR);
            default:
                throw ErrorHelper.Forbidden(
                    "You must complete this program before leaving a review.",
                    REVIEW_NOT_ELIGIBLE);
        }
    }
}
