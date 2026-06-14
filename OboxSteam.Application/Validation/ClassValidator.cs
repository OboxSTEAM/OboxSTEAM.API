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

        if (mentor.Role is not (RoleType.Mentor or RoleType.Manager or RoleType.SuperAdmin))
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

        if (request.MentorId == Guid.Empty)
        {
            throw ErrorHelper.BadRequest("MentorId is required.");
        }

        ValidateDateRange(request.StartDate, request.EndDate);
        ValidateMaxCapacity(request.MaxCapacity);
        ValidateMinHoursBeforeAssignmentJoin(request.MinHoursBeforeAssignmentJoin);
    }

    public static void ValidateDateRange(DateTime startDate, DateTime endDate)
    {
        if (endDate <= startDate)
        {
            throw ErrorHelper.BadRequest("EndDate must be after StartDate.");
        }
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
        ValidateMaxCapacity(entity.MaxCapacity);
        ValidateMinHoursBeforeAssignmentJoin(entity.MinHoursBeforeAssignmentJoin);
    }

    public static void ValidateStatusTransition(ClassStatus currentStatus, ClassStatus targetStatus)
    {
        var isValid = (currentStatus, targetStatus) switch
        {
            (ClassStatus.Draft, ClassStatus.Open) => true,
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

        if (targetStatus == ClassStatus.Open)
        {
            ValidateReadyToOpen(entity);
        }
    }

    public static void ValidateNotUpdatingStatusViaPatch(ClassStatus? requestedStatus)
    {
        if (requestedStatus.HasValue)
        {
            throw ErrorHelper.BadRequest(
                "Class status cannot be changed via update. Use Open, Start, or Complete endpoints.");
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
}
