using OboxSteam.Application.Utils;
using OboxSteam.Domain.Entities;
using OboxSteam.Domain.Enums;
using OboxSteam.Domain.Interfaces;

namespace OboxSteam.Application.Validation;

/// <summary>
/// Activity progress business rules and input validation.
/// </summary>
public static class ActivityProgressValidator
{
    public const string StartForbiddenMessage = "Only students can start activity progress.";
    public const string UpdateForbiddenMessage = "Only students can update activity progress.";

    public static void ValidateModuleEnrollmentIdRequired(Guid moduleEnrollmentId)
    {
        if (moduleEnrollmentId == Guid.Empty)
        {
            throw ErrorHelper.BadRequest("ModuleEnrollmentId is required.");
        }
    }

    public static void ValidateActivityIdRequired(Guid activityId)
    {
        if (activityId == Guid.Empty)
        {
            throw ErrorHelper.BadRequest("ActivityId is required.");
        }
    }

    public static ModuleEnrollment ValidateModuleEnrollmentExists(
        ModuleEnrollment? moduleEnrollment,
        Guid moduleEnrollmentId)
    {
        if (moduleEnrollment == null || moduleEnrollment.IsDeleted)
        {
            throw ErrorHelper.NotFound($"Module enrollment with id '{moduleEnrollmentId}' not found.");
        }

        return moduleEnrollment;
    }

    public static void ValidateModuleEnrollmentBelongsToStudent(
        ModuleEnrollment moduleEnrollment,
        Guid studentId)
    {
        if (moduleEnrollment.StudentId != studentId)
        {
            throw ErrorHelper.Forbidden("This module enrollment does not belong to the current student.");
        }
    }

    public static void ValidateModuleEnrollmentActive(ModuleEnrollment moduleEnrollment)
    {
        if (moduleEnrollment.Status != EnrollmentStatus.Active)
        {
            throw ErrorHelper.BadRequest("Module enrollment must be active to track activity progress.");
        }
    }

    public static Activity ValidateActivityExists(Activity? activity, Guid activityId)
    {
        if (activity == null || activity.IsDeleted)
        {
            throw ErrorHelper.NotFound($"Activity with id '{activityId}' not found.");
        }

        return activity;
    }

    public static void ValidateActivityBelongsToModule(Activity activity, Course course, Guid moduleId)
    {
        if (course.IsDeleted)
        {
            throw ErrorHelper.NotFound($"Course for activity '{activity.Id}' not found.");
        }

        if (course.ModuleId != moduleId)
        {
            throw ErrorHelper.BadRequest("Activity does not belong to the enrolled module.");
        }
    }

    public static void ValidateNoDuplicateProgress(ActivityProgress? existingProgress)
    {
        if (existingProgress != null)
        {
            throw ErrorHelper.Conflict("Activity progress already exists for this module enrollment.");
        }
    }

    public static ActivityProgress ValidateActivityProgressExists(
        ActivityProgress? activityProgress,
        Guid activityProgressId)
    {
        if (activityProgress == null || activityProgress.IsDeleted)
        {
            throw ErrorHelper.NotFound($"Activity progress with id '{activityProgressId}' not found.");
        }

        return activityProgress;
    }

    public static ActivityProgress ValidateActivityProgressForModuleEnrollment(
        ActivityProgress? activityProgress,
        Guid moduleEnrollmentId,
        Guid activityId)
    {
        if (activityProgress == null || activityProgress.IsDeleted)
        {
            throw ErrorHelper.NotFound(
                $"Activity progress for module enrollment '{moduleEnrollmentId}' and activity '{activityId}' not found.");
        }

        return activityProgress;
    }

    public static void ValidateActivityProgressBelongsToStudent(
        ActivityProgress activityProgress,
        Guid studentId)
    {
        if (activityProgress.StudentId != studentId)
        {
            throw ErrorHelper.Forbidden("This activity progress does not belong to the current student.");
        }
    }

    public static async Task<List<Guid>> GetModuleActivityIdsAsync(IUnitOfWork unitOfWork, Guid moduleId)
    {
        var courses = await unitOfWork.Courses.GetAllAsync(
            c => c.ModuleId == moduleId && !c.IsDeleted);

        if (courses.Count == 0)
        {
            return [];
        }

        var courseIds = courses.Select(c => c.Id).ToList();
        var activities = await unitOfWork.Activities.GetAllAsync(
            a => courseIds.Contains(a.CourseId) && !a.IsDeleted);

        return activities.Select(a => a.Id).ToList();
    }
}
