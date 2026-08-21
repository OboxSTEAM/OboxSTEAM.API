using OboxSteam.Application.Interfaces;
using OboxSteam.Application.Utils;
using OboxSteam.Domain.Entities;
using OboxSteam.Domain.Enums;
using OboxSteam.Domain.Interfaces;

namespace OboxSteam.Application.Validation;

/// <summary>
/// Week-bound rules and viewer access for the Monday–Sunday timetable.
/// </summary>
public static class ScheduleValidator
{
    public const string TimezoneId = "Asia/Ho_Chi_Minh";
    public const string WindowsTimezoneId = "SE Asia Standard Time";
    public const string ViewForbiddenMessage = "Only students and verified parents can view a weekly schedule.";

    public static void ValidateWeekStartIsMonday(DateOnly weekStart)
    {
        if (weekStart.DayOfWeek != DayOfWeek.Monday)
        {
            throw ErrorHelper.BadRequest(
                "weekStart must be a Monday (yyyy-MM-dd) in Asia/Ho_Chi_Minh.");
        }
    }

    /// <summary>
    /// Students view their own schedule. Parents must pass a verified linked <paramref name="studentId"/>.
    /// </summary>
    public static async Task<User> ResolveScheduleOwnerAsync(
        IUnitOfWork unitOfWork,
        IClaimsService claimsService,
        Guid? studentId)
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
            if (studentId.HasValue && studentId.Value != user.Id)
            {
                throw ErrorHelper.Forbidden("Students can only view their own schedule.");
            }

            return user;
        }

        if (user.Role == RoleType.Parent)
        {
            if (!studentId.HasValue || studentId.Value == Guid.Empty)
            {
                throw ErrorHelper.BadRequest("studentId is required to view a child's schedule.");
            }

            var (student, _) = await EnrollmentAccessValidator.EnsureVerifiedParentOfAsync(
                unitOfWork,
                claimsService,
                studentId.Value);
            return student;
        }

        throw ErrorHelper.Forbidden(ViewForbiddenMessage);
    }
}
