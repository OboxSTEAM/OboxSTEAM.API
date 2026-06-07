using OboxSteam.Application.Interfaces;
using OboxSteam.Application.Utils;
using OboxSteam.Domain.Entities;
using OboxSteam.Domain.Enums;
using OboxSteam.Domain.Interfaces;

namespace OboxSteam.Application.Validation;

/// <summary>
/// Shared authorization rules for program and module enrollment operations.
/// </summary>
public static class EnrollmentAccessValidator
{
    public static async Task<User> GetCurrentStudentForEnrollAsync(
        IUnitOfWork unitOfWork,
        IClaimsService claimsService,
        string enrollForbiddenMessage)
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

        if (user.Role != RoleType.Student)
        {
            throw ErrorHelper.Forbidden(enrollForbiddenMessage);
        }

        return user;
    }

    public static async Task<User> GetCurrentUserForGetAsync(
        IUnitOfWork unitOfWork,
        IClaimsService claimsService,
        string viewPermissionDeniedMessage)
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

        if (user.Role is not (RoleType.Student or RoleType.Parent or RoleType.SuperAdmin or RoleType.Manager))
        {
            throw ErrorHelper.Forbidden(viewPermissionDeniedMessage);
        }

        return user;
    }

    public static async Task EnsureCanViewEnrollmentAsync(
        IUnitOfWork unitOfWork,
        IClaimsService claimsService,
        Guid studentId,
        string viewEnrollmentForbiddenMessage)
    {
        var currentUserId = claimsService.GetCurrentUserId;
        if (currentUserId == Guid.Empty)
        {
            throw ErrorHelper.Unauthorized("Unauthorized access.");
        }

        if (currentUserId == studentId)
        {
            return;
        }

        var currentUser = await unitOfWork.Users.GetByIdAsync(currentUserId);
        if (currentUser == null || currentUser.IsDeleted)
        {
            throw ErrorHelper.NotFound("Current user not found.");
        }

        if (currentUser.Role is RoleType.SuperAdmin or RoleType.Manager)
        {
            return;
        }

        if (currentUser.Role == RoleType.Parent)
        {
            var parentLink = await unitOfWork.ParentStudents.FirstOrDefaultAsync(
                ps => ps.ParentId == currentUserId && ps.StudentId == studentId && !ps.IsDeleted);

            if (parentLink != null)
            {
                return;
            }

            throw ErrorHelper.Forbidden("You can only view enrollments of students linked to your account.");
        }

        throw ErrorHelper.Forbidden(viewEnrollmentForbiddenMessage);
    }
}
