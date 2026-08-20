using OboxSteam.Application.DTOs.ClassDTO;
using OboxSteam.Application.Utils;
using OboxSteam.Domain.Entities;
using OboxSteam.Domain.Enums;

namespace OboxSteam.Application.Validation;

/// <summary>
/// Class (cohort) business rules: dates, capacity, references, and status transitions.
/// </summary>
public static class ClassValidator
{
    public static void ValidatePagination(int page, int pageSize)
    {
        if (page < 1 || pageSize < 1)
        {
            throw ErrorHelper.BadRequest("Invalid pagination parameters. Page and pageSize must be at least 1.");
        }
    }

    public static void ValidateClassExists(Class? entity, Guid id)
    {
        if (entity == null || entity.IsDeleted)
        {
            throw ErrorHelper.NotFound($"Class with id '{id}' not found.");
        }
    }

    public static void ValidateProgramExists(Program? program, Guid programId)
    {
        if (program == null || program.IsDeleted)
        {
            throw ErrorHelper.NotFound($"Program with id '{programId}' not found.");
        }
    }

    public static void ValidateMentorExists(User? mentor, Guid mentorId)
    {
        if (mentor == null || mentor.IsDeleted)
        {
            throw ErrorHelper.NotFound($"Mentor with id '{mentorId}' not found.");
        }

        if (mentor.Role is not (RoleType.Mentor or RoleType.Manager or RoleType.Admin))
        {
            throw ErrorHelper.BadRequest($"User '{mentorId}' is not eligible to mentor a class.");
        }
    }

    public static void ValidateCreateRequest(CreateClassRequestDto request)
    {
        if (string.IsNullOrWhiteSpace(request.Code))
        {
            throw ErrorHelper.BadRequest("Code is required.");
        }

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            throw ErrorHelper.BadRequest("Name is required.");
        }

        if (request.ProgramId == Guid.Empty)
        {
            throw ErrorHelper.BadRequest("ProgramId is required.");
        }

        if (request.MentorId.HasValue && request.MentorId.Value == Guid.Empty)
        {
            throw ErrorHelper.BadRequest("MentorId cannot be an empty GUID.");
        }

        ValidateDateRange(request.StartDate, request.EndDate);
        ValidateStartDateLeadTime(request.StartDate, DateTime.UtcNow);
        ValidateMaxCapacity(request.MaxCapacity);
        ValidateMinHoursBeforeAssignmentJoin(request.MinHoursBeforeAssignmentJoin);
    }

    /// <summary>
    /// Minimum gap between class creation and StartDate so enrollment has a real window
    /// (students must be able to discover the class, enroll, and be confirmed before day one).
    /// </summary>
    public const int MinStartDateLeadTimeDays = 14;

    public static void ValidateStartDateLeadTime(DateTime startDate, DateTime utcNow)
    {
        var earliest = utcNow.Date.AddDays(MinStartDateLeadTimeDays);
        if (startDate < earliest)
        {
            throw ErrorHelper.BadRequest(
                $"StartDate must be at least {MinStartDateLeadTimeDays} days in the future " +
                $"(earliest allowed: {earliest:yyyy-MM-dd}) to leave room for enrollment.");
        }
    }

    public static void ValidateDateRange(DateTime startDate, DateTime endDate)
    {
        if (endDate <= startDate)
        {
            throw ErrorHelper.BadRequest("EndDate must be after StartDate.");
        }
    }

    /// <summary>
    /// Changing class dates must not orphan existing sessions: every active session has to
    /// stay inside the new range, otherwise the manager must cancel/reschedule them first.
    /// Without this the schedule silently "leaks" outside the class window while the
    /// coverage counts still match.
    /// </summary>
    public static void ValidateDateRangeCoversSessions(
        DateTime startDate,
        DateTime endDate,
        IReadOnlyCollection<ClassSession> activeSessions)
    {
        var offenders = activeSessions
            .Where(s => s.StartTime < startDate || s.EndTime > endDate)
            .OrderBy(s => s.StartTime)
            .ToList();

        if (offenders.Count == 0)
        {
            return;
        }

        var preview = string.Join(", ", offenders
            .Take(3)
            .Select(s => $"'{s.Title}' ({s.StartTime:yyyy-MM-dd HH:mm})"));

        throw ErrorHelper.BadRequest(
            $"{offenders.Count} active session(s) fall outside the new class date range: {preview}. " +
            "Cancel or reschedule those sessions before changing the class dates.");
    }

    public static void ValidateMaxCapacity(int maxCapacity)
    {
        if (maxCapacity < 1)
        {
            throw ErrorHelper.BadRequest("MaxCapacity must be at least 1.");
        }
    }

    public static void ValidateMinHoursBeforeAssignmentJoin(int minHours)
    {
        if (minHours < 0)
        {
            throw ErrorHelper.BadRequest("MinHoursBeforeAssignmentJoin cannot be negative.");
        }
    }

    public static void ValidateCapacityNotBelowEnrollment(int maxCapacity, int enrolledCount)
    {
        if (maxCapacity < enrolledCount)
        {
            throw ErrorHelper.BadRequest(
                $"MaxCapacity ({maxCapacity}) cannot be less than current enrollment count ({enrolledCount}).");
        }
    }

    public static void ValidateReadyToOpen(Class entity)
    {
        ValidateDateRange(entity.StartDate, entity.EndDate);

        // A Draft class can sit long enough for its StartDate to lapse — ReadyForMentor
        // and Open both require a still-future window.
        if (entity.StartDate <= DateTime.UtcNow)
        {
            throw ErrorHelper.BadRequest(
                "StartDate has already passed — move the class start date to the future before continuing.");
        }

        ValidateMaxCapacity(entity.MaxCapacity);
        ValidateMinHoursBeforeAssignmentJoin(entity.MinHoursBeforeAssignmentJoin);
    }

    /// <summary>
    /// ReadyForMentor requires a complete timetable. Mentor assignment happens on this
    /// status; students still cannot enroll.
    /// </summary>
    public static void ValidateReadyForMentorRequirements(int activeSessionCount, int schedulableItemCount)
    {
        if (activeSessionCount == 0)
        {
            throw ErrorHelper.BadRequest(
                "Generate the class schedule before marking it ready for mentor assignment.");
        }

        if (activeSessionCount != schedulableItemCount)
        {
            throw ErrorHelper.BadRequest(
                $"The schedule no longer matches the curriculum ({activeSessionCount} sessions " +
                $"for {schedulableItemCount} schedulable items). The curriculum changed after the " +
                "schedule was generated — delete the existing sessions and generate a new schedule, " +
                "or add the missing sessions manually.");
        }
    }

    /// <summary>
    /// A class only opens for student enrollment once students can see the full picture:
    /// an assigned mentor and a generated schedule that still covers the whole curriculum
    /// (every LiveOnline/Offline activity plus every assignment).
    /// </summary>
    public static void ValidateOpenRequirements(Class entity, int activeSessionCount, int schedulableItemCount)
    {
        if (entity.MentorId is null)
        {
            throw ErrorHelper.BadRequest(
                "Assign a mentor to the class before opening enrollment.");
        }

        ValidateReadyForMentorRequirements(activeSessionCount, schedulableItemCount);
    }

    public static void ValidateStatusTransition(ClassStatus currentStatus, ClassStatus targetStatus)
    {
        var isValid = (currentStatus, targetStatus) switch
        {
            (ClassStatus.Draft, ClassStatus.ReadyForMentor) => true,
            (ClassStatus.ReadyForMentor, ClassStatus.Open) => true,
            (ClassStatus.Open, ClassStatus.InProgress) => true,
            (ClassStatus.InProgress, ClassStatus.Completed) => true,
            _ => false,
        };

        if (!isValid)
        {
            throw ErrorHelper.BadRequest(
                $"Cannot transition class from '{currentStatus}' to '{targetStatus}'.");
        }
    }

    public static void ValidateTransitionToStatus(Class? entity, Guid id, ClassStatus targetStatus)
    {
        ValidateClassExists(entity, id);
        ValidateStatusTransition(entity!.Status, targetStatus);

        if (targetStatus is ClassStatus.Open or ClassStatus.ReadyForMentor)
        {
            ValidateReadyToOpen(entity);
        }
    }

    public static void ValidateNotUpdatingStatusViaPatch(ClassStatus? requestedStatus)
    {
        if (requestedStatus.HasValue)
        {
            throw ErrorHelper.BadRequest(
                "Class status cannot be changed via update. Use ReadyForMentor, Open, Start, or Complete endpoints.");
        }
    }

    /// <summary>
    /// Returns true when an Open class has reached capacity and its configured start time has arrived.
    /// </summary>
    public static bool IsReadyForAutoStart(Class classEntity, int activeEnrollmentCount, DateTime utcNow)
    {
        return classEntity.Status == ClassStatus.Open
               && activeEnrollmentCount >= classEntity.MaxCapacity
               && utcNow >= classEntity.StartDate;
    }

    public const string DeleteForbiddenMessage = "Only managers can delete a class.";

    public static void ValidateDeletableStatus(Class classEntity)
    {
        if (classEntity.Status is not (ClassStatus.Draft or ClassStatus.ReadyForMentor or ClassStatus.Open))
        {
            throw ErrorHelper.BadRequest(
                $"Only Draft, ReadyForMentor, or Open classes can be deleted (status: {classEntity.Status}).");
        }
    }

    public static void ValidateOpenClassHasNoActiveStudents(Class classEntity, int activeEnrollmentCount)
    {
        if (classEntity.Status == ClassStatus.Open && activeEnrollmentCount > 0)
        {
            throw ErrorHelper.Conflict(
                $"Cannot delete Open class '{classEntity.Code}' while it still has active students.");
        }
    }
}
