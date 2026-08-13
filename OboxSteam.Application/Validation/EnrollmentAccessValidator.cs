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

    public static async Task<User> GetCurrentManagerAsync(
        IUnitOfWork unitOfWork,
        IClaimsService claimsService,
        string forbiddenMessage)
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

        if (user.Role != RoleType.Manager)
        {
            throw ErrorHelper.Forbidden(forbiddenMessage);
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

        if (user.Role is not (RoleType.Student or RoleType.Parent or RoleType.Admin or RoleType.Manager))
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

        if (currentUser.Role is RoleType.Admin or RoleType.Manager)
        {
            return;
        }

        if (currentUser.Role == RoleType.Parent)
        {
            var parentLink = await unitOfWork.ParentStudents.FirstOrDefaultAsync(
                ps => ps.ParentId == currentUserId
                      && ps.StudentId == studentId
                      && ps.IsVerified
                      && !ps.IsDeleted);

            if (parentLink != null)
            {
                return;
            }

            throw ErrorHelper.Forbidden("You can only view enrollments of students linked to your account.");
        }

        throw ErrorHelper.Forbidden(viewEnrollmentForbiddenMessage);
    }

    /// <summary>
    /// Requires the caller to be a Parent with a verified link to <paramref name="studentId"/>.
    /// Returns the student user and the verified parent-student link.
    /// </summary>
    public static async Task<(User Student, ParentStudent Link)> EnsureVerifiedParentOfAsync(
        IUnitOfWork unitOfWork,
        IClaimsService claimsService,
        Guid studentId)
    {
        var currentUserId = claimsService.GetCurrentUserId;
        if (currentUserId == Guid.Empty)
        {
            throw ErrorHelper.Unauthorized("Unauthorized access.");
        }

        var currentUser = await unitOfWork.Users.GetByIdAsync(currentUserId);
        if (currentUser == null || currentUser.IsDeleted)
        {
            throw ErrorHelper.NotFound("Current user not found.");
        }

        if (currentUser.Role != RoleType.Parent)
        {
            throw ErrorHelper.Forbidden("Only parents can access this resource.");
        }

        var student = await unitOfWork.Users.GetByIdAsync(studentId);
        if (student == null || student.IsDeleted || student.Role != RoleType.Student)
        {
            throw ErrorHelper.NotFound($"Student '{studentId}' not found.");
        }

        var parentLink = await unitOfWork.ParentStudents.FirstOrDefaultAsync(
            ps => ps.ParentId == currentUserId
                  && ps.StudentId == studentId
                  && ps.IsVerified
                  && !ps.IsDeleted);

        if (parentLink == null)
        {
            throw ErrorHelper.Forbidden("You can only view progress of students with a verified link to your account.");
        }

        return (student, parentLink);
    }
}
