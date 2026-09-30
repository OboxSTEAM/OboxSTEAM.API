using OboxSteam.Application.Interfaces;
using OboxSteam.Application.Utils;
using OboxSteam.Domain.Entities;
using OboxSteam.Domain.Enums;
using OboxSteam.Domain.Interfaces;

namespace OboxSteam.Application.Validation;

/// <summary>
/// Advisory participants of a program: Manager and Admin, the advisor expert, and experts
/// on the program board.
/// </summary>
public static class AdvisoryParticipantGuard
{
    private const string NotMemberMessage = "You are not a member of this program advisory team.";

    public static async Task<(Program Program, User Actor)> RequireAsync(
        IUnitOfWork unitOfWork,
        IClaimsService claimsService,
        Guid programId)
    {
        var actorId = claimsService.GetCurrentUserId;
        if (actorId == Guid.Empty)
        {
            throw ErrorHelper.Unauthorized("Authenticated user id is unavailable.");
        }

        var actor = await unitOfWork.Users.GetByIdAsync(actorId);
        if (actor == null || actor.IsDeleted)
        {
            throw ErrorHelper.Unauthorized("Authenticated user was not found.");
        }

        var program = await unitOfWork.Programs.GetByIdAsync(programId);
        if (program == null || program.IsDeleted)
        {
            throw ErrorHelper.NotFound($"Program with id '{programId}' not found.");
        }

        if (actor.Role is RoleType.Manager or RoleType.Admin)
        {
            return (program, actor);
        }

        if (actor.Role != RoleType.Expert)
        {
            throw ErrorHelper.Forbidden(NotMemberMessage);
        }

        var expert = await unitOfWork.Experts.FirstOrDefaultAsync(e => e.UserId == actor.Id && !e.IsDeleted);
        if (expert == null)
        {
            throw ErrorHelper.Forbidden(NotMemberMessage);
        }

        if (program.AdvisorExpertId == expert.Id)
        {
            return (program, actor);
        }

        var boardMember = await unitOfWork.ProgramBoards.FirstOrDefaultAsync(
            b => b.ProgramId == programId && b.ExpertId == expert.Id && !b.IsDeleted);
        if (boardMember == null)
        {
            throw ErrorHelper.Forbidden(NotMemberMessage);
        }

        return (program, actor);
    }
}
