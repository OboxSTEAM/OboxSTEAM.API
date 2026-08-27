using OboxSteam.Application.Utils;
using OboxSteam.Domain.Entities;
using OboxSteam.Domain.Enums;
using OboxSteam.Domain.Interfaces;

namespace OboxSteam.Application.Validation;

/// <summary>
/// Class enrollment business rules and input validation.
/// </summary>
public static class ClassEnrollmentValidator
{
    public const string EnrollForbiddenMessage = "Only students can enroll in a class.";
    public const string ManagerTransferForbiddenMessage = "Only managers can transfer a student to another class.";
    public const string ViewListForbiddenMessage = "You do not have permission to view class enrollments.";
    public const string ViewEnrollmentForbiddenMessage = "You do not have permission to view this enrollment.";

    /// <summary>Soft product cap: Active class enrollments per student across all programs.</summary>
    public const int MaxActiveClassesPerStudent = 2;

    public static void ValidateProgramEnrollmentIdRequired(Guid programEnrollmentId)
    {
        if (programEnrollmentId == Guid.Empty)
        {
            throw ErrorHelper.BadRequest("ProgramEnrollmentId is required.");
        }
    }

    public static void ValidateClassIdRequired(Guid classId)
    {
        if (classId == Guid.Empty)
        {
            throw ErrorHelper.BadRequest("ClassId is required.");
        }
    }

    public static ProgramEnrollment ValidateProgramEnrollmentExists(
        ProgramEnrollment? programEnrollment,
        Guid programEnrollmentId)
    {
        if (programEnrollment == null || programEnrollment.IsDeleted)
        {
            throw ErrorHelper.NotFound($"Program enrollment with id '{programEnrollmentId}' not found.");
        }

        return programEnrollment;
    }

    public static void ValidateProgramEnrollmentBelongsToStudent(
        ProgramEnrollment programEnrollment,
        Guid studentId)
    {
        if (programEnrollment.StudentId != studentId)
        {
            throw ErrorHelper.Forbidden("This program enrollment does not belong to the current student.");
        }
    }

    public static void ValidateProgramEnrollmentActiveForEnroll(ProgramEnrollment programEnrollment)
    {
        if (programEnrollment.Status != EnrollmentStatus.Active)
        {
            throw ErrorHelper.BadRequest("Program enrollment must be active to enroll in a class.");
        }
    }

    public static Class ValidateClassExists(Class? classEntity, Guid classId)
    {
        ClassValidator.ValidateClassExists(classEntity, classId);
        return classEntity!;
    }

    public static void ValidateClassBelongsToProgram(Class classEntity, Guid programId)
    {
        if (classEntity.ProgramId != programId)
        {
            throw ErrorHelper.BadRequest("Class does not belong to the enrolled program.");
        }
    }

    /// <summary>
    /// Self-enroll and student transfer only while the cohort is Open (recruiting).
    /// InProgress means teaching has started — no new joins via student flows.
    /// </summary>
    public static void ValidateClassOpenForEnrollment(Class classEntity)
    {
        if (classEntity.Status != ClassStatus.Open)
        {
            throw ErrorHelper.BadRequest(
                $"Class '{classEntity.Code}' is not open for enrollment (status: {classEntity.Status}).");
        }
    }

    /// <summary>
    /// Manager transfer target must be Open (not yet started).
    /// </summary>
    public static void ValidateClassOpenForManagerTransfer(Class classEntity)
    {
        if (classEntity.Status != ClassStatus.Open)
        {
            throw ErrorHelper.BadRequest(
                $"Class '{classEntity.Code}' must be Open and not yet started (status: {classEntity.Status}).");
        }
    }

    public static void ValidateStudentIdRequired(Guid studentId)
    {
        if (studentId == Guid.Empty)
        {
            throw ErrorHelper.BadRequest("StudentId is required.");
        }
    }

    public static ClassEnrollment ValidateActiveClassEnrollmentForProgram(
        ClassEnrollment? enrollment,
        Guid studentId,
        Guid programId)
    {
        if (enrollment == null)
        {
            throw ErrorHelper.NotFound(
                $"No active class enrollment found for student '{studentId}' in program '{programId}'.");
        }

        return enrollment;
    }

    public static void ValidateNoActiveClassEnrollmentForProgram(ClassEnrollment? activeEnrollment)
    {
        if (activeEnrollment != null)
        {
            throw ErrorHelper.Conflict("You already have an active class enrollment for this program.");
        }
    }

    public static ClassEnrollment ValidateClassEnrollmentExists(
        ClassEnrollment? enrollment,
        Guid enrollmentId)
    {
        if (enrollment == null || enrollment.IsDeleted)
        {
            throw ErrorHelper.NotFound($"Class enrollment with id '{enrollmentId}' not found.");
        }

        return enrollment;
    }

    public static void ValidateClassEnrollmentBelongsToStudent(
        ClassEnrollment enrollment,
        Guid studentId)
    {
        if (enrollment.StudentId != studentId)
        {
            throw ErrorHelper.Forbidden("This class enrollment does not belong to the current student.");
        }
    }

    public static void ValidateEnrollmentActive(ClassEnrollment enrollment)
    {
        if (enrollment.Status != ClassEnrollmentStatus.Active)
        {
            throw ErrorHelper.BadRequest("Only an active class enrollment can be transferred.");
        }
    }

    public static void ValidateTransferTargetDifferent(Guid currentClassId, Guid targetClassId)
    {
        if (currentClassId == targetClassId)
        {
            throw ErrorHelper.BadRequest("Target class must differ from the current class.");
        }
    }

    public static void ValidateNotAlreadyEnrolledInClass(ClassEnrollment? existingEnrollment, Guid currentEnrollmentId)
    {
        if (existingEnrollment != null && existingEnrollment.Id != currentEnrollmentId)
        {
            throw ErrorHelper.Conflict("You are already enrolled in the target class.");
        }
    }

    public static async Task<int> GetActiveSeatsTakenAsync(IUnitOfWork unitOfWork, Guid classId)
    {
        var activeEnrollments = await unitOfWork.ClassEnrollments.GetAllAsync(
            ce => ce.ClassId == classId
                  && ce.Status == ClassEnrollmentStatus.Active
                  && !ce.IsDeleted);

        return activeEnrollments.Count;
    }

    /// <summary>Alias for <see cref="GetActiveSeatsTakenAsync"/> — Active seats only (no soft hold).</summary>
    public static Task<int> GetSeatsTakenAsync(IUnitOfWork unitOfWork, Guid classId)
        => GetActiveSeatsTakenAsync(unitOfWork, classId);

    public static async Task ValidateClassHasCapacityAsync(
        IUnitOfWork unitOfWork,
        Guid classId,
        int maxCapacity)
    {
        var activeEnrollmentCount = await GetSeatsTakenAsync(unitOfWork, classId);

        if (activeEnrollmentCount >= maxCapacity)
        {
            throw ErrorHelper.Conflict("Class has reached maximum capacity.");
        }
    }

    public static async Task ValidateLateJoinAllowedAsync(IUnitOfWork unitOfWork, Class classEntity)
    {
        var now = DateTime.UtcNow;

        var nextAssignmentWindow = unitOfWork.ClassSessions
            .GetQueryable()
            .Where(cs => cs.ClassId == classEntity.Id
                         && cs.SessionKind == SessionKind.AssignmentWindow
                         && cs.StartTime > now
                         && cs.Status != ClassSessionStatus.Cancelled
                         && !cs.IsDeleted)
            .OrderBy(cs => cs.StartTime)
            .FirstOrDefault();

        if (nextAssignmentWindow == null)
        {
            return;
        }

        var hoursUntil = (nextAssignmentWindow.StartTime - now).TotalHours;
        if (hoursUntil < classEntity.MinHoursBeforeAssignmentJoin)
        {
            throw ErrorHelper.BadRequest(
                $"Cannot join within {classEntity.MinHoursBeforeAssignmentJoin} hours of the next assignment window.");
        }
    }

    /// <summary>
    /// Ensures the student is under the active primary class enrollment cap.
    /// Pass <paramref name="excludeEnrollmentId"/> when transferring so the current seat is not double-counted.
    /// </summary>
    public static Task ValidateUnderActiveClassLimitAsync(
        IUnitOfWork unitOfWork,
        Guid studentId,
        Guid? excludeEnrollmentId = null)
        => StudentLoadValidator.ValidateUnderPrimaryClassLoadAsync(
            unitOfWork,
            studentId,
            excludeEnrollmentId);
}
