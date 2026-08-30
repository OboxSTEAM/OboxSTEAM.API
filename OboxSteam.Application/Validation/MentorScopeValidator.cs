using OboxSteam.Application.Utils;
using OboxSteam.Domain.Entities;
using OboxSteam.Domain.Enums;
using OboxSteam.Domain.Interfaces;

namespace OboxSteam.Application.Validation;

/// <summary>
/// Mentor authorization and scheduling rules scoped to a specific class cohort.
/// </summary>
public static class MentorScopeValidator
{
    public const string OwnsClassForbiddenMessage =
        "You can only manage resources for classes where you are the assigned mentor.";

    public const string StudentNotInMentorClassMessage =
        "You can only manage students enrolled in a class where you are the assigned mentor.";

    public const string ClassIdRequiredMessage =
        "ClassId is required for mentor operations.";

    public const string ClassProgramMismatchMessage =
        "The class does not belong to the same program as this module.";

    public const string OwnsProgramForbiddenMessage =
        "You can only manage curriculum for programs where you currently teach an assigned class.";

    public static async Task EnsureMentorOwnsProgramAsync(
        IUnitOfWork unitOfWork,
        Guid mentorId,
        Guid programId)
    {
        var ownsClass = await unitOfWork.Classes.FirstOrDefaultAsync(
            c => c.MentorId == mentorId
                 && c.ProgramId == programId
                 && !c.IsDeleted);

        if (ownsClass == null)
        {
            throw ErrorHelper.Forbidden(OwnsProgramForbiddenMessage);
        }
    }

    public static async Task EnsureMentorOwnsAssignmentAsync(
        IUnitOfWork unitOfWork,
        Guid mentorId,
        Assignment assignment)
    {
        var module = await unitOfWork.Modules.GetByIdAsync(assignment.ModuleId);
        if (module == null || module.IsDeleted)
        {
            throw ErrorHelper.NotFound($"Module with id '{assignment.ModuleId}' not found.");
        }

        await EnsureMentorOwnsProgramAsync(unitOfWork, mentorId, module.ProgramId);
    }

    public static async Task<Class> EnsureMentorOwnsClassAsync(
        IUnitOfWork unitOfWork,
        Guid mentorId,
        Guid classId)
    {
        var classEntity = await unitOfWork.Classes.GetByIdAsync(classId);
        ClassValidator.ValidateClassExists(classEntity, classId);

        if (classEntity!.MentorId != mentorId)
        {
            throw ErrorHelper.Forbidden(OwnsClassForbiddenMessage);
        }

        return classEntity;
    }

    public static async Task<Class> EnsureMentorOwnsClassForModuleAsync(
        IUnitOfWork unitOfWork,
        Guid mentorId,
        Guid classId,
        Guid moduleId)
    {
        var classEntity = await EnsureMentorOwnsClassAsync(unitOfWork, mentorId, classId);

        var module = await unitOfWork.Modules.GetByIdAsync(moduleId);
        if (module == null || module.IsDeleted)
        {
            throw ErrorHelper.NotFound($"Module with id '{moduleId}' not found.");
        }

        if (module.ProgramId != classEntity.ProgramId)
        {
            throw ErrorHelper.BadRequest(ClassProgramMismatchMessage);
        }

        return classEntity;
    }

    public static async Task<ClassEnrollment> EnsureMentorOwnsStudentInProgramAsync(
        IUnitOfWork unitOfWork,
        Guid mentorId,
        Guid studentId,
        Guid programId)
    {
        var programEnrollment = await unitOfWork.ProgramEnrollments.FirstOrDefaultAsync(
            pe => pe.StudentId == studentId
                  && pe.ProgramId == programId
                  && !pe.IsDeleted
                  && pe.Status == EnrollmentStatus.Active);

        if (programEnrollment == null)
        {
            throw ErrorHelper.Forbidden(StudentNotInMentorClassMessage);
        }

        var classEnrollment = await unitOfWork.ClassEnrollments.FirstOrDefaultAsync(
            ce => ce.StudentId == studentId
                  && ce.ProgramEnrollmentId == programEnrollment.Id
                  && ce.Status == ClassEnrollmentStatus.Active
                  && !ce.IsDeleted);

        if (classEnrollment == null)
        {
            throw ErrorHelper.Forbidden(StudentNotInMentorClassMessage);
        }

        var classEntity = await unitOfWork.Classes.GetByIdAsync(classEnrollment.ClassId);
        ClassValidator.ValidateClassExists(classEntity, classEnrollment.ClassId);

        if (classEntity!.MentorId != mentorId)
        {
            throw ErrorHelper.Forbidden(StudentNotInMentorClassMessage);
        }

        return classEnrollment;
    }

    /// <summary>
    /// Ensures a mentor has no overlapping class sessions in the proposed time range.
    /// Soft-deleted sessions are ignored by the global query filter.
    /// Cancelled sessions are ignored because they no longer block the calendar.
    /// </summary>
    /// <param name="pendingMentorReassignmentClassId">
    /// When reassigning a class mentor before save, sessions in this class still block
    /// even though the class row in the database still has the previous mentor.
    /// </param>
    public static async Task ValidateMentorSessionNoOverlapAsync(
        IUnitOfWork unitOfWork,
        Guid mentorId,
        DateTime startTime,
        DateTime endTime,
        Guid? excludeSessionId = null,
        Guid? pendingMentorReassignmentClassId = null)
    {
        if (endTime <= startTime)
        {
            throw ErrorHelper.BadRequest("EndTime must be after StartTime.");
        }

        var overlappingSession = await unitOfWork.ClassSessions.FirstOrDefaultAsync(
            cs => cs.Status != ClassSessionStatus.Cancelled
                  && cs.SessionKind != SessionKind.AssignmentWindow
                  && cs.StartTime < endTime
                  && cs.EndTime > startTime
                  && (!excludeSessionId.HasValue || cs.Id != excludeSessionId.Value)
                  && (cs.Class.MentorId == mentorId
                      || (pendingMentorReassignmentClassId.HasValue
                          && cs.ClassId == pendingMentorReassignmentClassId.Value)),
            cs => cs.Class);

        if (overlappingSession != null)
        {
            throw ErrorHelper.Conflict(
                $"Mentor schedule overlaps with session '{overlappingSession.Title}' " +
                $"in class '{overlappingSession.Class.Code}' " +
                $"({overlappingSession.StartTime:yyyy-MM-dd HH:mm} – {overlappingSession.EndTime:yyyy-MM-dd HH:mm} UTC).");
        }
    }

    /// <summary>
    /// Validates that a new mentor can take over a class without session-time conflicts.
    /// </summary>
    public static async Task ValidateMentorCanTakeClassSessionsAsync(
        IUnitOfWork unitOfWork,
        Guid mentorId,
        Guid classId)
    {
        var sessions = await unitOfWork.ClassSessions.GetAllAsync(
            cs => cs.ClassId == classId && cs.Status != ClassSessionStatus.Cancelled);

        foreach (var session in sessions)
        {
            if (session.SessionKind == SessionKind.AssignmentWindow)
            {
                continue;
            }

            await ValidateMentorSessionNoOverlapAsync(
                unitOfWork,
                mentorId,
                session.StartTime,
                session.EndTime,
                excludeSessionId: session.Id,
                pendingMentorReassignmentClassId: classId);
        }
    }
}
