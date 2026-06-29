using OboxSteam.Application.Interfaces;
using OboxSteam.Application.Utils;
using OboxSteam.Domain.Entities;
using OboxSteam.Domain.Enums;
using OboxSteam.Domain.Interfaces;

namespace OboxSteam.Application.Validation;

/// <summary>
/// Authorization rules for viewing a class student roster.
/// </summary>
public static class ClassRosterValidator
{
    public const string ViewClassRosterForbiddenMessage =
        "You do not have permission to view this class student roster.";

    public static async Task EnsureCanViewClassRosterAsync(
        IUnitOfWork unitOfWork,
        IClaimsService claimsService,
        Class classEntity)
    {
        var userId = claimsService.GetCurrentUserId;
        if (userId == Guid.Empty)
        {
            throw ErrorHelper.Unauthorized("Unauthorized access.");
        }

        var user = await unitOfWork.Users.GetByIdAsync(userId);
        if (user == null || user.IsDeleted)
        {
            throw ErrorHelper.NotFound("Current user not found.");
        }

        if (user.Role == RoleType.Student)
        {
            throw ErrorHelper.Forbidden(ViewClassRosterForbiddenMessage);
        }

        if (user.Role is RoleType.SuperAdmin or RoleType.Manager)
        {
            return;
        }

        if (user.Role == RoleType.Mentor)
        {
            if (classEntity.MentorId != user.Id)
            {
                throw ErrorHelper.Forbidden(MentorScopeValidator.OwnsClassForbiddenMessage);
            }

            return;
        }

        throw ErrorHelper.Forbidden(ViewClassRosterForbiddenMessage);
    }
}
