using OboxSteam.Application.DTOs.SessionAttendanceDTO;
using OboxSteam.Application.Interfaces;
using OboxSteam.Application.Utils;
using OboxSteam.Domain.Entities;
using OboxSteam.Domain.Enums;
using OboxSteam.Domain.Interfaces;

namespace OboxSteam.Application.Validation;

/// <summary>
/// Session attendance business rules and authorization.
/// </summary>
public static class SessionAttendanceValidator
{
    public const string ViewSessionAttendanceForbiddenMessage =
        "You can only view your own session attendance.";

    public const string ViewSessionRosterForbiddenMessage =
        "You do not have permission to view this session attendance roster.";

    public const string UpdateSessionAttendanceForbiddenMessage =
        "Only Mentor, Manager, and Admin can update session attendance.";

    public static void ValidateSessionAttendanceExists(SessionAttendance? entity, Guid id)
    {
        if (entity == null || entity.IsDeleted)
        {
            throw ErrorHelper.NotFound($"Session attendance with id '{id}' not found.");
        }
    }

    public static void ValidateUpdateRequest(UpdateSessionAttendanceRequestDto request)
    {
        if (!Enum.IsDefined(request.Status))
        {
            throw ErrorHelper.BadRequest("Invalid attendance status.");
        }
    }

    public static async Task<User> GetCurrentUserAsync(
        IUnitOfWork unitOfWork,
        IClaimsService claimsService)
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

        return user;
    }

    public static async Task<User> EnsureCanViewSessionRosterAsync(
        IUnitOfWork unitOfWork,
        IClaimsService claimsService,
        ClassSession classSession)
    {
        var user = await GetCurrentUserAsync(unitOfWork, claimsService);

        if (user.Role == RoleType.Student)
        {
            return user;
        }

        if (user.Role is RoleType.Admin or RoleType.Manager)
        {
            return user;
        }

        if (user.Role == RoleType.Mentor)
        {
            await EnsureMentorCanManageClassSessionAsync(unitOfWork, user.Id, classSession);
            return user;
        }

        throw ErrorHelper.Forbidden(ViewSessionRosterForbiddenMessage);
    }

    public static async Task EnsureCanViewSessionAttendanceAsync(
        IUnitOfWork unitOfWork,
        IClaimsService claimsService,
        SessionAttendance attendance,
        ClassSession classSession)
    {
        var userId = claimsService.GetCurrentUserId;
        if (userId == Guid.Empty)
        {
            throw ErrorHelper.Unauthorized("Unauthorized access.");
        }

        if (attendance.StudentId == userId)
        {
            return;
        }

        var user = await GetCurrentUserAsync(unitOfWork, claimsService);

        if (user.Role is RoleType.Admin or RoleType.Manager)
        {
            return;
        }

        if (user.Role == RoleType.Mentor)
        {
            await EnsureMentorCanManageClassSessionAsync(unitOfWork, user.Id, classSession);
            return;
        }

        throw ErrorHelper.Forbidden(ViewSessionAttendanceForbiddenMessage);
    }

    public static async Task<User> EnsureCanUpdateSessionAttendanceAsync(
        IUnitOfWork unitOfWork,
        IClaimsService claimsService,
        ClassSession classSession)
    {
        var user = await GetCurrentUserAsync(unitOfWork, claimsService);

        if (user.Role is RoleType.Admin or RoleType.Manager)
        {
            return user;
        }

        if (user.Role == RoleType.Mentor)
        {
            await EnsureMentorCanManageClassSessionAsync(unitOfWork, user.Id, classSession);
            return user;
        }

        throw ErrorHelper.Forbidden(UpdateSessionAttendanceForbiddenMessage);
    }

    public static async Task EnsureMentorCanManageClassSessionAsync(
        IUnitOfWork unitOfWork,
        Guid mentorId,
        ClassSession classSession)
    {
        await MentorScopeValidator.EnsureMentorOwnsClassAsync(
            unitOfWork,
            mentorId,
            classSession.ClassId);
    }
}
