using OboxSteam.Application.Utils;
using OboxSteam.Domain.Entities;
using OboxSteam.Domain.Enums;
using OboxSteam.Domain.Interfaces;

namespace OboxSteam.Application.Validation;

public static class ProgramAdvisorResolver
{
    private const string InvalidAdvisorMessage = "Advisor must be an active expert with a linked login.";

    /// <summary>Returns the expert when it is active and has an active Expert login; null input returns null.</summary>
    public static async Task<Expert?> ResolveAsync(IUnitOfWork unitOfWork, Guid? advisorExpertId)
    {
        if (!advisorExpertId.HasValue)
        {
            return null;
        }

        if (advisorExpertId.Value == Guid.Empty)
        {
            throw ErrorHelper.BadRequest("AdvisorExpertId cannot be empty.");
        }

        var expert = await unitOfWork.Experts.GetByIdAsync(advisorExpertId.Value);
        if (expert == null || expert.IsDeleted || !expert.UserId.HasValue)
        {
            throw ErrorHelper.BadRequest(InvalidAdvisorMessage);
        }

        if (!await HasActiveLoginAsync(unitOfWork, expert))
        {
            throw ErrorHelper.BadRequest(InvalidAdvisorMessage);
        }

        return expert;
    }

    public static async Task<bool> HasActiveLoginAsync(IUnitOfWork unitOfWork, Expert expert)
    {
        if (expert.IsDeleted || !expert.UserId.HasValue)
        {
            return false;
        }

        var user = await unitOfWork.Users.GetByIdAsync(expert.UserId.Value);
        return user is { IsDeleted: false, Role: RoleType.Expert, Status: AccountStatus.Active };
    }
}
