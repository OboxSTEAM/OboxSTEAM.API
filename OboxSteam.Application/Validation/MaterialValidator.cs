using OboxSteam.Application.Utils;
using OboxSteam.Domain.Entities;
using OboxSteam.Domain.Enums;

namespace OboxSteam.Application.Validation;

/// <summary>
/// Material business rules — SelfPaced activities only, one material per activity.
/// </summary>
public static class MaterialValidator
{
    public static void ValidateActivityExists(Activity? activity, Guid activityId)
    {
        if (activity == null || activity.IsDeleted)
        {
            throw ErrorHelper.NotFound($"Activity with id '{activityId}' not found.");
        }
    }

    public static void ValidateSelfPacedOnly(Activity activity)
    {
        if (activity.ActivityType != ActivityType.SelfPaced)
        {
            throw ErrorHelper.BadRequest("Materials are only supported for SelfPaced activities.");
        }
    }
}
