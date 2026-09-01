using OboxSteam.Application.Utils;
using OboxSteam.Domain.Entities;
using OboxSteam.Domain.Enums;

namespace OboxSteam.Application.Validation;

/// <summary>
/// Business rules for Offline co-teach invitations on a class session.
/// </summary>
public static class ClassSessionExpertValidator
{
    public static void ValidatePagination(int page, int pageSize)
        => ClassMentorRequestValidator.ValidatePagination(page, pageSize);

    public static void ValidateInvitationExists(ClassSessionExpert? invitation, Guid id)
    {
        if (invitation == null || invitation.IsDeleted)
        {
            throw ErrorHelper.NotFound($"Class session expert invitation with id '{id}' not found.");
        }
    }

    public static void ValidateOfflineScheduledSession(ClassSession session)
    {
        if (session.SessionKind != SessionKind.Offline)
        {
            throw ErrorHelper.BadRequest(
                "Experts can only be invited to Offline sessions.");
        }

        if (session.Status != ClassSessionStatus.Scheduled)
        {
            throw ErrorHelper.Conflict(
                $"Experts can only be invited to Scheduled sessions (status: {session.Status}).");
        }
    }

    public static void ValidateExpertCanLogin(Expert expert)
    {
        if (!expert.UserId.HasValue || expert.UserId.Value == Guid.Empty)
        {
            throw ErrorHelper.BadRequest(
                $"Expert '{expert.Code}' has no login account and cannot receive invitations.");
        }
    }

    public static void ValidateExpertOnProgramBoard(ProgramBoard? board, Expert expert)
    {
        if (board == null || board.IsDeleted)
        {
            throw ErrorHelper.BadRequest(
                $"Expert '{expert.Code}' is not on the program board and cannot be invited.");
        }
    }

    public static void ValidateNoActiveExpertOnSession(ClassSessionExpert? existing)
    {
        if (existing != null)
        {
            throw ErrorHelper.Conflict(
                "This session already has an invited or accepted expert. Withdraw or wait for a decline before inviting another.");
        }
    }

    public static void ValidateOwnership(ClassSessionExpert invitation, Guid expertId)
    {
        if (invitation.ExpertId != expertId)
        {
            throw ErrorHelper.Forbidden("You can only manage your own co-teach invitations.");
        }
    }

    public static void ValidateInvitedForDecision(ClassSessionExpert invitation)
    {
        if (invitation.Status != ClassSessionExpertStatus.Invited)
        {
            throw ErrorHelper.BadRequest(
                $"Only Invited invitations can be accepted or declined (status: {invitation.Status}).");
        }
    }

    public static void ValidateInvitedForWithdraw(ClassSessionExpert invitation)
    {
        if (invitation.Status != ClassSessionExpertStatus.Invited)
        {
            throw ErrorHelper.Conflict(
                "An accepted co-teach invitation cannot be withdrawn.");
        }
    }

    public static void ValidateSessionStillScheduled(ClassSession session)
    {
        if (session.Status != ClassSessionStatus.Scheduled)
        {
            throw ErrorHelper.Conflict(
                $"This session is no longer Scheduled (status: {session.Status}).");
        }
    }

    public static void ValidateAcceptedForRescheduleDecision(ClassSessionExpert invitation)
    {
        if (invitation.Status != ClassSessionExpertStatus.Accepted)
        {
            throw ErrorHelper.BadRequest(
                $"Only an Accepted expert can approve or decline a reschedule (status: {invitation.Status}).");
        }
    }

    public static void ValidatePendingReschedule(ClassSession session)
    {
        if (!session.ProposedStartTime.HasValue || !session.ProposedEndTime.HasValue)
        {
            throw ErrorHelper.BadRequest("This session has no pending reschedule to decide.");
        }
    }
}
