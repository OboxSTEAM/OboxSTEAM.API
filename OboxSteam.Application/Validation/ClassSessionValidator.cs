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

        ValidateExactlyOneCurriculumItem(request.ActivityId, request.AssignmentId);

        var now = DateTime.UtcNow;
        if (request.StartTime <= now)
        {
            throw ErrorHelper.BadRequest("StartTime cannot be in the past.");
        }

        // Assignment windows need an explicit end; activity ends are derived later from DurationMinutes.
        if (request.AssignmentId.HasValue)
        {
            if (!request.EndTime.HasValue)
            {
                throw ErrorHelper.BadRequest(
                    "EndTime is required for assignment sessions (assignments have no DurationMinutes).");
            }

            ScheduleTimeValidator.ValidateFutureRange(request.StartTime, request.EndTime.Value);
        }
    }

    /// <summary>
    /// Session kind is derived from the curriculum item — same mapping as generate:
    /// LiveOnline activity → LiveOnline, Offline activity → Offline, Assignment → AssignmentWindow.
    /// </summary>
    public static SessionKind ResolveSessionKind(Activity? activity, bool forAssignment)
    {
        if (forAssignment)
        {
            return SessionKind.AssignmentWindow;
        }

        if (activity == null)
        {
            throw ErrorHelper.BadRequest("Activity is required to resolve SessionKind.");
        }

        return activity.ActivityType == ActivityType.Offline
            ? SessionKind.Offline
            : SessionKind.LiveOnline;
    }

    public static void ValidateSessionKindNotOverridden(SessionKind? requestedSessionKind)
    {
        if (requestedSessionKind.HasValue)
        {
            throw ErrorHelper.BadRequest(
                "SessionKind is derived from the curriculum item " +
                "(LiveOnline → LiveOnline, Offline → Offline, Assignment → AssignmentWindow) " +
                "and cannot be set manually.");
        }
    }

    /// <summary>
    /// Activity session length always comes from the curriculum template.
    /// </summary>
    public static DateTime ResolveActivitySessionEnd(DateTime startTime, Activity activity)
    {
        if (activity.DurationMinutes is null or <= 0)
        {
            throw ErrorHelper.BadRequest(
                $"Activity '{activity.Name}' ({activity.Code}) has no DurationMinutes. " +
                "Set a positive duration on the activity before scheduling it.");
        }

        return startTime.AddMinutes(activity.DurationMinutes.Value);
    }

    public static void ValidateActivitySessionEndNotOverridden(DateTime? requestedEndTime)
    {
        if (requestedEndTime.HasValue)
        {
            throw ErrorHelper.BadRequest(
                "Cannot set EndTime on an activity session — end is derived from the activity's " +
                "DurationMinutes. Change StartTime to reschedule.");
        }
    }

    /// <summary>
    /// A session maps to exactly one curriculum item (one activity XOR one assignment),
    /// mirroring what session generation produces. Ad-hoc multi-purpose sessions are not
    /// allowed — extra teaching content must enter through the curriculum itself.
    /// </summary>
    public static void ValidateExactlyOneCurriculumItem(Guid? activityId, Guid? assignmentId)
    {
        if (!activityId.HasValue && !assignmentId.HasValue)
        {
            throw ErrorHelper.BadRequest("At least one of ActivityId or AssignmentId must be provided.");
        }

        if (activityId.HasValue && assignmentId.HasValue)
        {
            throw ErrorHelper.BadRequest(
                "Provide either ActivityId or AssignmentId, not both — a session maps to exactly one curriculum item.");
        }
    }

    /// <summary>
    /// Geo coordinates are optional but must come as a pair within valid ranges.
    /// </summary>
    public static void ValidateCoordinates(double? latitude, double? longitude)
    {
        if (latitude.HasValue != longitude.HasValue)
        {
            throw ErrorHelper.BadRequest("Latitude and Longitude must be provided together.");
        }

        if (latitude is < -90 or > 90)
        {
            throw ErrorHelper.BadRequest("Latitude must be between -90 and 90.");
        }

        if (longitude is < -180 or > 180)
        {
            throw ErrorHelper.BadRequest("Longitude must be between -180 and 180.");
        }
    }

    public static void ValidateGenerateRequest(GenerateClassSessionsRequestDto request)
    {
        if (request.DaysOfWeek == null || request.DaysOfWeek.Count == 0)
        {
            throw ErrorHelper.BadRequest("At least one day of week is required.");
        }

        if (request.DaysOfWeek.Any(d => !Enum.IsDefined(d)))
        {
            throw ErrorHelper.BadRequest("DaysOfWeek contains an invalid day.");
        }

        if (request.SessionEndTime <= request.SessionStartTime)
        {
            throw ErrorHelper.BadRequest("SessionEndTime must be after SessionStartTime.");
        }
    }

    public static void ValidateModuleBelongsToClass(Module module, Class classEntity)
    {
        if (module.ProgramId != classEntity.ProgramId)
        {
            throw ErrorHelper.BadRequest("Module does not belong to the class program.");
        }

        if (classEntity.Kind == ClassKind.Remedial
            && classEntity.RemedialModuleId.HasValue
            && module.Id != classEntity.RemedialModuleId.Value)
        {
            throw ErrorHelper.BadRequest(
                "Remedial classes can only schedule sessions for their remedial module.");
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

    public static async Task<Activity> ValidateActivityBelongsToModuleAsync(
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

        return activity;
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
        Guid? assignmentId,
        Guid? excludeSessionId = null)
    {
        var module = await unitOfWork.Modules.GetByIdAsync(moduleId);
        var validatedModule = AssignmentValidator.ValidateModuleExists(module);
        ValidateModuleBelongsToClass(validatedModule, classEntity);

        if (activityId.HasValue)
        {
            var activity = await ValidateActivityBelongsToModuleAsync(
                unitOfWork, activityId.Value, moduleId);

            if (activity.ActivityType == ActivityType.SelfPaced)
            {
                throw ErrorHelper.BadRequest(
                    "Self-paced activities are not schedulable — students complete them without a class session.");
            }
        }

        if (assignmentId.HasValue)
        {
            await ValidateAssignmentBelongsToModuleAsync(unitOfWork, assignmentId.Value, moduleId);
        }

        ValidateNoDuplicateItemSession(
            unitOfWork, classEntity.Id, activityId, assignmentId, excludeSessionId);
    }

    /// <summary>
    /// One curriculum item has at most one active session per class — sessions must stick
    /// to the curriculum. To add sessions, extend the curriculum first (which is guarded
    /// by <see cref="CurriculumEditGuard"/>); to redo one, cancel the old session first.
    /// </summary>
    private static void ValidateNoDuplicateItemSession(
        IUnitOfWork unitOfWork,
        Guid classId,
        Guid? activityId,
        Guid? assignmentId,
        Guid? excludeSessionId)
    {
        var duplicateExists = unitOfWork.ClassSessions
            .GetQueryable()
            .Any(s => s.ClassId == classId
                      && !s.IsDeleted
                      && s.Status != ClassSessionStatus.Cancelled
                      && (excludeSessionId == null || s.Id != excludeSessionId.Value)
                      && ((activityId.HasValue && s.ActivityId == activityId)
                          || (assignmentId.HasValue && s.AssignmentId == assignmentId)));

        if (duplicateExists)
        {
            throw ErrorHelper.Conflict(
                "This curriculum item already has an active session in this class. " +
                "Cancel the existing session first, or extend the curriculum to add more sessions.");
        }
    }
}
