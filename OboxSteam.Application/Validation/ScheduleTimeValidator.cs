using OboxSteam.Application.Utils;

namespace OboxSteam.Application.Validation;

/// <summary>
/// Reusable validation for scheduled start/end times (must be in the future, end after start).
/// </summary>
public static class ScheduleTimeValidator
{
    public static void ValidateFutureRange(
        DateTime? startTime,
        DateTime? endTime,
        string startFieldName = "StartTime",
        string endFieldName = "EndTime",
        DateTime? utcNow = null)
    {
        if (!startTime.HasValue || !endTime.HasValue)
        {
            return;
        }

        var now = utcNow ?? DateTime.UtcNow;

        if (startTime.Value <= now)
        {
            throw ErrorHelper.BadRequest($"{startFieldName} cannot be in the past.");
        }

        if (endTime.Value <= now)
        {
            throw ErrorHelper.BadRequest($"{endFieldName} cannot be in the past.");
        }

        if (endTime.Value <= startTime.Value)
        {
            throw ErrorHelper.BadRequest($"{endFieldName} must be after {startFieldName}.");
        }
    }

    public static void ValidateBothRequired(DateTime? startTime, DateTime? endTime)
    {
        if (!startTime.HasValue || !endTime.HasValue)
        {
            throw ErrorHelper.BadRequest("Start time and end time are required.");
        }
    }

    public static void ValidateRequiredIfAnyProvided(DateTime? startTime, DateTime? endTime)
    {
        var hasStart = startTime.HasValue;
        var hasEnd = endTime.HasValue;

        if (hasStart != hasEnd)
        {
            throw ErrorHelper.BadRequest("Both start time and end time must be provided together.");
        }
    }
}
