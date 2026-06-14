using OboxSteam.Application.DTOs.ClassSessionDTO;
using OboxSteam.Application.Utils;
using OboxSteam.Domain.Entities;
using OboxSteam.Domain.Enums;
using OboxSteam.Domain.Interfaces;

namespace OboxSteam.Application.Validation;

/// <summary>
/// Class session business rules: scheduling, references, and status transitions.
/// </summary>
public static class ClassSessionValidator
{
    public static void ValidatePagination(int page, int pageSize)
    {
        if (page < 1 || pageSize < 1)
        {
            throw ErrorHelper.BadRequest("Invalid pagination parameters. Page and pageSize must be at least 1.");
        }
    }

    public static void ValidateClassSessionExists(ClassSession? entity, Guid id)
    {
        if (entity == null || entity.IsDeleted)
        {
            throw ErrorHelper.NotFound($"Class session with id '{id}' not found.");
        }
    }

    public static void ValidateCreateRequest(CreateClassSessionRequestDto request)
    {
        if (request.ClassId == Guid.Empty)
        {
            throw ErrorHelper.BadRequest("ClassId is required.");
        }

        if (request.ModuleId == Guid.Empty)
        {
            throw ErrorHelper.BadRequest("ModuleId is required.");
        }

        if (string.IsNullOrWhiteSpace(request.Title))
        {
            throw ErrorHelper.BadRequest("Title is required.");
        }

        ValidateActivityOrAssignmentRequired(request.ActivityId, request.AssignmentId);
        ScheduleTimeValidator.ValidateFutureRange(request.StartTime, request.EndTime);
        ValidateMaxCapacity(request.MaxCapacity);
    }

    public static void ValidateActivityOrAssignmentRequired(Guid? activityId, Guid? assignmentId)
    {
        if (!activityId.HasValue && !assignmentId.HasValue)
        {
            throw ErrorHelper.BadRequest("At least one of ActivityId or AssignmentId must be provided.");
        }
    }

    public static void ValidateMaxCapacity(int? maxCapacity)
    {
        if (maxCapacity.HasValue && maxCapacity.Value < 1)
        {
            throw ErrorHelper.BadRequest("MaxCapacity must be at least 1 when provided.");
        }
    }

    public static void ValidateModuleBelongsToClass(Module module, Class classEntity)
    {
        if (module.ProgramId != classEntity.ProgramId)
        {
            throw ErrorHelper.BadRequest("Module does not belong to the class program.");
        }
    }

    public static void ValidateSessionWithinClassDateRange(Class classEntity, DateTime startTime, DateTime endTime)
    {
        if (startTime < classEntity.StartDate || endTime > classEntity.EndDate)
        {
            throw ErrorHelper.BadRequest("Session must fall within the class start and end dates.");
        }
    }

    public static void ValidateClassSchedulable(Class classEntity)
    {
        if (classEntity.Status == ClassStatus.Completed)
        {
            throw ErrorHelper.BadRequest("Cannot schedule sessions for a completed class.");
        }
    }

    public static void ValidateSessionModifiable(ClassSession session)
    {
        if (session.Status is ClassSessionStatus.Completed or ClassSessionStatus.Cancelled)
        {
            throw ErrorHelper.BadRequest(
                $"Cannot modify a class session with status '{session.Status}'.");
        }
    }

    public static void ValidateStatusTransition(ClassSessionStatus currentStatus, ClassSessionStatus targetStatus)
    {
        if (currentStatus == targetStatus)
        {
            return;
        }

        var isValid = (currentStatus, targetStatus) switch
        {
            (ClassSessionStatus.Scheduled, ClassSessionStatus.InProgress) => true,
            (ClassSessionStatus.Scheduled, ClassSessionStatus.Cancelled) => true,
            (ClassSessionStatus.InProgress, ClassSessionStatus.Completed) => true,
            (ClassSessionStatus.InProgress, ClassSessionStatus.Cancelled) => true,
            _ => false,
        };

        if (!isValid)
        {
            throw ErrorHelper.BadRequest(
                $"Cannot transition class session from '{currentStatus}' to '{targetStatus}'.");
        }
    }

    public static async Task ValidateActivityBelongsToModuleAsync(
        IUnitOfWork unitOfWork,
        Guid activityId,
        Guid moduleId)
    {
        var activity = await unitOfWork.Activities.GetByIdAsync(activityId);
        if (activity == null || activity.IsDeleted)
        {
            throw ErrorHelper.NotFound($"Activity with id '{activityId}' not found.");
        }

        var course = await unitOfWork.Courses.GetByIdAsync(activity.CourseId);
        ActivityValidator.ValidateCourseExists(course, activity.CourseId);

        if (course!.ModuleId != moduleId)
        {
            throw ErrorHelper.BadRequest("Activity does not belong to the specified module.");
        }
    }

    public static async Task ValidateAssignmentBelongsToModuleAsync(
        IUnitOfWork unitOfWork,
        Guid assignmentId,
        Guid moduleId)
    {
        var assignment = await unitOfWork.Assignments.GetByIdAsync(assignmentId);
        if (assignment == null || assignment.IsDeleted)
        {
            throw ErrorHelper.NotFound($"Assignment with id '{assignmentId}' not found.");
        }

        if (assignment.ModuleId != moduleId)
        {
            throw ErrorHelper.BadRequest("Assignment does not belong to the specified module.");
        }
    }

    public static async Task ValidateReferencesAsync(
        IUnitOfWork unitOfWork,
        Class classEntity,
        Guid moduleId,
        Guid? activityId,
        Guid? assignmentId)
    {
        var module = await unitOfWork.Modules.GetByIdAsync(moduleId);
        var validatedModule = AssignmentValidator.ValidateModuleExists(module);
        ValidateModuleBelongsToClass(validatedModule, classEntity);

        if (activityId.HasValue)
        {
            await ValidateActivityBelongsToModuleAsync(unitOfWork, activityId.Value, moduleId);
        }

        if (assignmentId.HasValue)
        {
            await ValidateAssignmentBelongsToModuleAsync(unitOfWork, assignmentId.Value, moduleId);
        }
    }
}
