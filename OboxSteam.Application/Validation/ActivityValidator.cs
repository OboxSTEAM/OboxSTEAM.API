using OboxSteam.Application.Utils;
using OboxSteam.Domain.Entities;
using OboxSteam.Domain.Enums;
using OboxSteam.Domain.Interfaces;

namespace OboxSteam.Application.Validation;

/// <summary>
/// Activity-specific business rules. Composes reusable validators for schedule and type.
/// </summary>
public static class ActivityValidator
{
    public static IReadOnlyCollection<ActivityType> GetAllowedActivityTypes(ModuleType moduleType) =>
        moduleType switch
        {
            ModuleType.Theory => [ActivityType.SelfPaced, ActivityType.LiveOnline],
            ModuleType.Experiential => [ActivityType.SelfPaced, ActivityType.LiveOnline, ActivityType.Offline],
            ModuleType.Research => [ActivityType.SelfPaced, ActivityType.LiveOnline, ActivityType.Offline],
            _ => throw ErrorHelper.BadRequest($"Unsupported module type '{moduleType}'.")
        };

    public static void ValidateActivityTypeForModule(ModuleType moduleType, ActivityType activityType)
    {
        var allowedTypes = GetAllowedActivityTypes(moduleType);
        if (allowedTypes.Contains(activityType))
        {
            return;
        }

        var allowedList = string.Join(", ", allowedTypes);
        throw ErrorHelper.BadRequest(
            $"ActivityType '{activityType}' is not allowed for {moduleType} module. Allowed: {allowedList}.");
    }

    public static async Task ValidateActivityTypeForCourseAsync(
        IUnitOfWork unitOfWork,
        Guid courseId,
        ActivityType activityType)
    {
        var course = await unitOfWork.Courses.GetByIdAsync(courseId);
        ValidateCourseExists(course, courseId);

        var module = await unitOfWork.Modules.GetByIdAsync(course!.ModuleId);
        var validModule = AssignmentValidator.ValidateModuleExists(module);
        ValidateActivityTypeForModule(validModule.ModuleType, activityType);
    }

    public static void ValidateCourseExists(Course? course, Guid courseId)
    {
        if (course == null || course.IsDeleted)
        {
            throw ErrorHelper.NotFound($"Course with id '{courseId}' not found.");
        }
    }

    /// <summary>
    /// Type-specific rules. Activities carry no schedule or venue — those live on the
    /// generated <see cref="ClassSession"/>; an activity only declares its session length.
    /// </summary>
    public static void ValidateTypeRules(
        ActivityType activityType,
        int? durationMinutes,
        bool requireQrCheckin)
    {
        switch (activityType)
        {
            case ActivityType.SelfPaced:
                if (durationMinutes.HasValue)
                {
                    throw ErrorHelper.BadRequest(
                        "DurationMinutes is not allowed for SelfPaced activities — they are never scheduled.");
                }

                if (requireQrCheckin)
                {
                    throw ErrorHelper.BadRequest("QR check-in is only allowed for Offline activities.");
                }

                break;

            case ActivityType.LiveOnline:
            case ActivityType.Offline:
                if (!durationMinutes.HasValue || durationMinutes.Value <= 0)
                {
                    throw ErrorHelper.BadRequest(
                        "DurationMinutes must be a positive number for LiveOnline and Offline activities.");
                }

                if (activityType == ActivityType.LiveOnline && requireQrCheckin)
                {
                    throw ErrorHelper.BadRequest("QR check-in is only allowed for Offline activities.");
                }

                break;

            default:
                throw ErrorHelper.BadRequest($"Unsupported activity type '{activityType}'.");
        }
    }
}
