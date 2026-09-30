using OboxSteam.Application.Interfaces;
using OboxSteam.Application.Utils;
using OboxSteam.Domain.Entities;
using OboxSteam.Domain.Enums;
using OboxSteam.Domain.Interfaces;

namespace OboxSteam.Application.Commons;

public enum AdvisoryParticipantRole
{
    Manager,
    Advisor,
    BoardExpert,
}

public sealed record AdvisoryParticipant(Program Program, User User, AdvisoryParticipantRole Role)
{
    public bool IsManager => Role == AdvisoryParticipantRole.Manager;

    /// <summary>Advisor or board expert: may pin, reopen, and resolve.</summary>
    public bool IsExpertParticipant => Role is AdvisoryParticipantRole.Advisor or AdvisoryParticipantRole.BoardExpert;
}

/// <summary>Participants of a program advisory chat: Manager/Admin, the advisor, and board experts.</summary>
public static class AdvisoryParticipantAccess
{
    private const string NotMemberMessage = "You are not a member of this program advisory team.";

    public static async Task<AdvisoryParticipant> RequireAsync(
        IUnitOfWork unitOfWork,
        IClaimsService claimsService,
        Guid programId)
    {
        var userId = claimsService.GetCurrentUserId;
        if (userId == Guid.Empty)
        {
            throw ErrorHelper.Unauthorized("Authenticated user id is unavailable.");
        }

        var user = await unitOfWork.Users.GetByIdAsync(userId);
        if (user == null || user.IsDeleted)
        {
            throw ErrorHelper.Unauthorized("Authenticated user was not found.");
        }

        var program = await unitOfWork.Programs.GetByIdAsync(programId);
        if (program == null || program.IsDeleted)
        {
            throw ErrorHelper.NotFound($"Program with id '{programId}' not found.");
        }

        if (user.Role is RoleType.Manager or RoleType.Admin)
        {
            return new AdvisoryParticipant(program, user, AdvisoryParticipantRole.Manager);
        }

        if (user.Role != RoleType.Expert)
        {
            throw ErrorHelper.Forbidden(NotMemberMessage);
        }

        var expert = await unitOfWork.Experts.FirstOrDefaultAsync(e => e.UserId == user.Id && !e.IsDeleted)
            ?? throw ErrorHelper.Forbidden(NotMemberMessage);
        if (program.AdvisorExpertId == expert.Id)
        {
            return new AdvisoryParticipant(program, user, AdvisoryParticipantRole.Advisor);
        }

        var boardMember = await unitOfWork.ProgramBoards.FirstOrDefaultAsync(
            b => b.ProgramId == programId && b.ExpertId == expert.Id && !b.IsDeleted);
        return boardMember != null
            ? new AdvisoryParticipant(program, user, AdvisoryParticipantRole.BoardExpert)
            : throw ErrorHelper.Forbidden(NotMemberMessage);
    }

    public static string DisplayName(User user)
        => string.IsNullOrWhiteSpace(user.FullName) ? user.Email : user.FullName;
}
