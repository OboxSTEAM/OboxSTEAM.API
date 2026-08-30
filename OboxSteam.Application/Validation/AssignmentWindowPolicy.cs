using OboxSteam.Application.Commons;
using OboxSteam.Application.Utils;
using OboxSteam.Domain.Entities;
using OboxSteam.Domain.Enums;
using OboxSteam.Domain.Interfaces;

namespace OboxSteam.Application.Validation;

/// <summary>
/// Resolves the per-class AssignmentWindow session that gates new assessment attempts.
/// An attempt already in progress continues after <c>EndTime</c> until the student
/// submits. AcademicFail treats a draft as continuation only while
/// <see cref="Submission.ExpiresAt"/> is set and still in the future.
/// </summary>
public static class AssignmentWindowPolicy
{
    public const string NotAvailableMessage = "This assignment is not available.";

    public const string NotYetOpenMessage = "Assignment is not yet available.";

    public const string ClosedMessage = "Assignment is no longer available.";

    public static bool IsActiveWindow(ClassSession session)
        => !session.IsDeleted
           && session.Status != ClassSessionStatus.Cancelled
           && session.SessionKind == SessionKind.AssignmentWindow
           && session.AssignmentId.HasValue;

    public static bool IsInProgressContinuation(Submission? submission)
        => submission is { IsDeleted: false }
           && submission.Status is SubmissionStatus.Pending or SubmissionStatus.ReturnedForRevision;

    /// <summary>
    /// In-progress draft that still blocks AcademicFail. Null or elapsed
    /// <see cref="Submission.ExpiresAt"/> is not a hold.
    /// </summary>
    public static bool IsBlockingInProgress(Submission? submission, DateTime utcNow)
    {
        if (!IsInProgressContinuation(submission))
        {
            return false;
        }

        if (!submission!.ExpiresAt.HasValue || submission.ExpiresAt.Value < utcNow)
        {
            return false;
        }

        return true;
    }

    public static string? GetNewAttemptBlockReason(ClassSession? window, DateTime utcNow)
    {
        if (window == null)
        {
            return NotAvailableMessage;
        }

        if (utcNow < window.StartTime)
        {
            return NotYetOpenMessage;
        }

        if (utcNow > window.EndTime)
        {
            return ClosedMessage;
        }

        return null;
    }

    public static async Task<ClassSession> ResolveForStudentAsync(
        IUnitOfWork unitOfWork,
        Guid assignmentId,
        Guid studentId)
    {
        var window = await TryGetForStudentAsync(unitOfWork, assignmentId, studentId);
        if (window == null)
        {
            throw ErrorHelper.Conflict(NotAvailableMessage);
        }

        return window;
    }

    public static async Task<ClassSession?> TryGetForStudentAsync(
        IUnitOfWork unitOfWork,
        Guid assignmentId,
        Guid studentId)
    {
        var classId = await TryGetStudentClassIdForAssignmentAsync(unitOfWork, assignmentId, studentId);
        if (!classId.HasValue)
        {
            return null;
        }

        return await TryGetForClassAsync(unitOfWork, classId.Value, assignmentId);
    }

    /// <summary>
    /// Class the student should use for this assignment: the Active/Deferred program
    /// enrollment's class seat (Primary first). Does not fall through to another class
    /// on the same program.
    /// </summary>
    public static async Task<Guid?> TryGetStudentClassIdForAssignmentAsync(
        IUnitOfWork unitOfWork,
        Guid assignmentId,
        Guid studentId)
    {
        var assignment = await unitOfWork.Assignments.GetByIdAsync(assignmentId);
        if (assignment == null || assignment.IsDeleted)
        {
            return null;
        }

        var module = await unitOfWork.Modules.GetByIdAsync(assignment.ModuleId);
        if (module == null || module.IsDeleted)
        {
            return null;
        }

        var programEnrollments = await unitOfWork.ProgramEnrollments.GetAllAsync(
            pe => pe.StudentId == studentId
                  && pe.ProgramId == module.ProgramId
                  && !pe.IsDeleted
                  && (pe.Status == EnrollmentStatus.Active || pe.Status == EnrollmentStatus.Deferred));

        var preferredPe = programEnrollments
            .OrderBy(pe => pe.Status == EnrollmentStatus.Active ? 0 : 1)
            .ThenByDescending(pe => pe.EnrolledAt)
            .FirstOrDefault();

        if (preferredPe != null)
        {
            var classId = await TryGetClassIdForProgramEnrollmentAsync(
                unitOfWork,
                studentId,
                preferredPe.Id);
            if (classId.HasValue)
            {
                var classEntity = await unitOfWork.Classes.GetByIdAsync(classId.Value);
                if (classEntity != null && !classEntity.IsDeleted && classEntity.ProgramId == module.ProgramId)
                {
                    return classId;
                }
            }
        }

        var enrollments = await unitOfWork.ClassEnrollments.GetAllAsync(
            ce => ce.StudentId == studentId
                  && ce.Status == ClassEnrollmentStatus.Active
                  && !ce.IsDeleted);

        foreach (var enrollment in enrollments.OrderBy(ce => ce.Kind == ClassEnrollmentKind.Primary ? 0 : 1))
        {
            var classEntity = await unitOfWork.Classes.GetByIdAsync(enrollment.ClassId);
            if (classEntity == null || classEntity.IsDeleted || classEntity.ProgramId != module.ProgramId)
            {
                continue;
            }

            return enrollment.ClassId;
        }

        return null;
    }

    public static async Task<Guid?> TryGetClassIdForProgramEnrollmentAsync(
        IUnitOfWork unitOfWork,
        Guid studentId,
        Guid programEnrollmentId)
    {
        var seats = await unitOfWork.ClassEnrollments.GetAllAsync(
            ce => ce.StudentId == studentId
                  && ce.ProgramEnrollmentId == programEnrollmentId
                  && ce.Status == ClassEnrollmentStatus.Active
                  && !ce.IsDeleted);

        return seats
            .OrderBy(ce => ce.Kind == ClassEnrollmentKind.Primary ? 0 : 1)
            .Select(ce => (Guid?)ce.ClassId)
            .FirstOrDefault();
    }

    public static async Task<ClassSession?> TryGetForClassAsync(
        IUnitOfWork unitOfWork,
        Guid classId,
        Guid assignmentId)
    {
        var sessions = await unitOfWork.ClassSessions.GetAllAsync(
            cs => cs.ClassId == classId
                  && cs.AssignmentId == assignmentId
                  && cs.SessionKind == SessionKind.AssignmentWindow
                  && cs.Status != ClassSessionStatus.Cancelled
                  && !cs.IsDeleted);

        return sessions.OrderBy(cs => cs.StartTime).FirstOrDefault();
    }

    public static async Task<Dictionary<Guid, ClassSession>> LoadWindowsByAssignmentIdAsync(
        IUnitOfWork unitOfWork,
        Guid classId)
    {
        var sessions = await unitOfWork.ClassSessions.GetAllAsync(
            cs => cs.ClassId == classId
                  && cs.SessionKind == SessionKind.AssignmentWindow
                  && cs.AssignmentId != null
                  && cs.Status != ClassSessionStatus.Cancelled
                  && !cs.IsDeleted);

        return sessions
            .GroupBy(cs => cs.AssignmentId!.Value)
            .ToDictionary(g => g.Key, g => g.OrderBy(cs => cs.StartTime).First());
    }

    public static async Task<Dictionary<Guid, ClassSession>> LoadWindowsForProgramEnrollmentAsync(
        IUnitOfWork unitOfWork,
        Guid studentId,
        Guid programEnrollmentId)
    {
        var classId = await TryGetClassIdForProgramEnrollmentAsync(
            unitOfWork,
            studentId,
            programEnrollmentId);
        if (!classId.HasValue)
        {
            return new Dictionary<Guid, ClassSession>();
        }

        return await LoadWindowsByAssignmentIdAsync(unitOfWork, classId.Value);
    }

    public static void EnsureOpenForNewAttempt(ClassSession window, DateTime utcNow)
        => EnsureAllowsNewAttempt(window, utcNow);

    public static void EnsureAllowsNewAttempt(ClassSession? window, DateTime utcNow)
    {
        var reason = GetNewAttemptBlockReason(window, utcNow);
        if (reason == null)
        {
            return;
        }

        if (reason == NotYetOpenMessage)
        {
            throw ErrorHelper.Forbidden(reason);
        }

        throw ErrorHelper.Conflict(reason);
    }

    public static bool IsOpen(ClassSession window, DateTime utcNow)
        => utcNow >= window.StartTime && utcNow <= window.EndTime;

    public static DateTime? WindowEnd(IReadOnlyDictionary<Guid, ClassSession> windows, Guid assignmentId)
        => windows.TryGetValue(assignmentId, out var window) ? window.EndTime : null;

    public static DateTime? WindowStart(IReadOnlyDictionary<Guid, ClassSession> windows, Guid assignmentId)
        => windows.TryGetValue(assignmentId, out var window) ? window.StartTime : null;

    /// <summary>
    /// Student curriculum nav: completed/submitted/prereq-locked stay as-is.
    /// In-progress drafts stay available after the window closes. New work is locked
    /// when the window is missing, not yet open, or already ended.
    /// </summary>
    public static string ApplyCalendarToStudentNavStatus(
        string status,
        ClassSession? window,
        IReadOnlyCollection<Submission>? submissions,
        DateTime utcNow)
    {
        if (status is CurriculumStatusHelper.StatusCompleted
            or CurriculumStatusHelper.StatusSubmitted
            or CurriculumStatusHelper.StatusLocked)
        {
            return status;
        }

        if (submissions != null && submissions.Any(IsInProgressContinuation))
        {
            return status;
        }

        return GetNewAttemptBlockReason(window, utcNow) == null
            ? status
            : CurriculumStatusHelper.StatusLocked;
    }
}
