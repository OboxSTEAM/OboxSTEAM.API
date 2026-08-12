using OboxSteam.Application.Utils;
using OboxSteam.Domain.Entities;
using OboxSteam.Domain.Enums;

namespace OboxSteam.Application.Validation;

/// <summary>
/// Student retrospective attempt rules (plain-text draft and submit flow).
/// </summary>
public static class RetrospectiveAttemptValidator
{
    public const string RetrospectiveForbiddenMessage = "Only students can work on retrospective assignments.";

    public static void ValidateAssignmentIdRequired(Guid assignmentId)
    {
        if (assignmentId == Guid.Empty)
        {
            throw ErrorHelper.BadRequest("AssignmentId is required.");
        }
    }

    public static Assignment ValidateAssignmentForRetrospective(Assignment? assignment)
    {
        if (assignment == null || assignment.IsDeleted)
        {
            throw ErrorHelper.NotFound("Assignment not found.");
        }

        if (assignment.AssignmentType != AssignmentType.Retrospective)
        {
            throw ErrorHelper.BadRequest("This assignment is not a retrospective.");
        }

        return assignment;
    }

    public static void ValidateAssignmentAvailability(
        Assignment assignment,
        DateTime utcNow,
        DateTime? personalDueDate = null,
        DateTime? personalAvailableUntil = null)
    {
        if (assignment.AvailableFrom.HasValue && utcNow < assignment.AvailableFrom.Value)
        {
            throw ErrorHelper.Forbidden("Assignment is not yet available.");
        }

        var effectiveUntil = AssessmentAttemptPolicy.ResolveEffectiveAvailableUntil(
            assignment,
            personalAvailableUntil);
        if (effectiveUntil.HasValue && utcNow > effectiveUntil.Value)
        {
            throw ErrorHelper.Conflict("Assignment is no longer available.");
        }

        var effectiveDue = AssessmentAttemptPolicy.ResolveEffectiveDueDate(assignment, personalDueDate);
        if (effectiveDue.HasValue && utcNow > effectiveDue.Value)
        {
            throw ErrorHelper.Conflict("Assignment is past due date.");
        }
    }

    public static Submission ValidateSubmissionExists(Submission? submission, Guid submissionId)
    {
        if (submission == null || submission.IsDeleted)
        {
            throw ErrorHelper.NotFound($"Submission with id '{submissionId}' not found.");
        }

        return submission;
    }

    public static void ValidateSubmissionOwnership(Submission submission, Guid studentId)
    {
        if (submission.StudentId != studentId)
        {
            throw ErrorHelper.Forbidden("You do not have access to this submission.");
        }
    }

    public static void ValidateSubmissionOpenForDraft(Submission submission)
    {
        if (submission.Status is not (SubmissionStatus.Pending or SubmissionStatus.ReturnedForRevision))
        {
            throw ErrorHelper.Conflict("This submission is not open for editing.");
        }
    }

    public static void ValidateSubmissionOpenForSubmit(Submission submission)
    {
        if (submission.Status is not (SubmissionStatus.Pending or SubmissionStatus.ReturnedForRevision))
        {
            throw ErrorHelper.Conflict("This submission is not open for submission.");
        }
    }

    public static void ValidateSubmissionNotResearch(Submission submission)
    {
        if (submission.ResearchMilestoneId.HasValue)
        {
            throw ErrorHelper.BadRequest(
                "This is a research submission. Use the research submission endpoints.");
        }
    }

    public static void ValidateCanStartOrResume(Submission submission)
    {
        if (submission.Status == SubmissionStatus.TurnedIn)
        {
            throw ErrorHelper.Conflict("Submission is pending mentor review.");
        }

        if (submission.Status == SubmissionStatus.Graded)
        {
            throw ErrorHelper.Conflict("This assignment has already been graded for the current module attempt.");
        }
    }

    public static void ValidateFinalContentText(string? contentText)
    {
        if (string.IsNullOrWhiteSpace(contentText))
        {
            throw ErrorHelper.BadRequest("ContentText is required to submit a retrospective.");
        }
    }

    public static string? NormalizeDraftContentText(string? contentText)
    {
        if (contentText == null)
        {
            return null;
        }

        var trimmed = contentText.Trim();
        return trimmed.Length == 0 ? null : trimmed;
    }
}
