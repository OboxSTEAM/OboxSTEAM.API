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
            var activeEnrollment = await unitOfWork.ClassEnrollments.FirstOrDefaultAsync(
                ce => ce.ClassId == classEntity.Id
                      && ce.StudentId == user.Id
                      && ce.Status == ClassEnrollmentStatus.Active
                      && !ce.IsDeleted);

            if (activeEnrollment == null)
            {
                throw ErrorHelper.Forbidden(ViewClassRosterForbiddenMessage);
            }

            return;
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
