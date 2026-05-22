using OboxSteam.Application.Utils;
using OboxSteam.Domain.Entities;
using OboxSteam.Domain.Enums;

namespace OboxSteam.Application.Validation;

/// <summary>
/// Activity-specific business rules. Composes reusable validators for schedule and type.
/// </summary>
public static class ActivityValidator
{
    public static void ValidateCourseExists(Course? course, Guid courseId)
    {
        if (course == null || course.IsDeleted)
        {
            throw ErrorHelper.NotFound($"Course with id '{courseId}' not found.");
        }
    }

    public static void ValidateTypeRules(
        ActivityType activityType,
        DateTime? startTime,
        DateTime? endTime,
        string? location,
        bool requireQrCheckin,
        DateTime? utcNow = null)
    {
        switch (activityType)
        {
            case ActivityType.SelfPaced:
                if (startTime.HasValue || endTime.HasValue)
                {
                    ScheduleTimeValidator.ValidateRequiredIfAnyProvided(startTime, endTime);
                    ScheduleTimeValidator.ValidateFutureRange(
                        startTime,
                        endTime,
                        startFieldName: "StartTime",
                        endFieldName: "EndTime",
                        utcNow: utcNow);
                }

                if (requireQrCheckin)
                {
                    throw ErrorHelper.BadRequest("QR check-in is only allowed for Offline activities.");
                }

                break;

            case ActivityType.LiveOnline:
            case ActivityType.Offline:
                if (!startTime.HasValue || !endTime.HasValue)
                {
                    throw ErrorHelper.BadRequest(
                        "StartTime and EndTime are required for LiveOnline and Offline activities.");
                }

                if (string.IsNullOrWhiteSpace(location))
                {
                    throw ErrorHelper.BadRequest(
                        "Location is required for LiveOnline and Offline activities.");
                }

                ScheduleTimeValidator.ValidateFutureRange(
                    startTime,
                    endTime,
                    startFieldName: "StartTime",
                    endFieldName: "EndTime",
                    utcNow: utcNow);

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
