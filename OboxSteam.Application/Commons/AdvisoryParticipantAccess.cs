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

        var role = await ResolveRoleAsync(unitOfWork, user, program)
            ?? throw ErrorHelper.Forbidden(NotMemberMessage);
        return new AdvisoryParticipant(program, user, role);
    }

    /// <summary>Non-throwing membership check for callers without an HTTP request, e.g. hub methods.</summary>
    public static async Task<bool> IsParticipantAsync(IUnitOfWork unitOfWork, Guid userId, Guid programId)
    {
        ArgumentNullException.ThrowIfNull(unitOfWork);
        if (userId == Guid.Empty || programId == Guid.Empty)
        {
            return false;
        }

        var user = await unitOfWork.Users.GetByIdAsync(userId);
        if (user == null || user.IsDeleted)
        {
            return false;
        }

        var program = await unitOfWork.Programs.GetByIdAsync(programId);
        if (program == null || program.IsDeleted)
        {
            return false;
        }

        return await ResolveRoleAsync(unitOfWork, user, program) != null;
    }

    private static async Task<AdvisoryParticipantRole?> ResolveRoleAsync(IUnitOfWork unitOfWork, User user, Program program)
    {
        if (user.Role is RoleType.Manager or RoleType.Admin)
        {
            return AdvisoryParticipantRole.Manager;
        }

        if (user.Role != RoleType.Expert)
        {
            return null;
        }

        var expert = await unitOfWork.Experts.FirstOrDefaultAsync(e => e.UserId == user.Id && !e.IsDeleted);
        if (expert == null)
        {
            return null;
        }

        if (program.AdvisorExpertId == expert.Id)
        {
            return AdvisoryParticipantRole.Advisor;
        }

        var boardMember = await unitOfWork.ProgramBoards.FirstOrDefaultAsync(
            b => b.ProgramId == program.Id && b.ExpertId == expert.Id && !b.IsDeleted);
        return boardMember != null ? AdvisoryParticipantRole.BoardExpert : null;
    }

    public static string DisplayName(User user)
        => string.IsNullOrWhiteSpace(user.FullName) ? user.Email : user.FullName;
}
