using OboxSteam.Domain.Entities;
using OboxSteam.Domain.Enums;
using OboxSteam.Domain.Interfaces;

namespace OboxSteam.Application.Validation;

/// <summary>
/// Shared attempt and personal-deadline rules for quiz / file / retrospective / research.
/// Theory modules: unlimited attempts. Other types: MaxAttempts + approved recovery grants.
/// </summary>
public static class AssessmentAttemptPolicy
{
    public const int MaxRecoveryRequestsPerAssignment = 2;

    public static async Task<Module?> GetModuleForAssignmentAsync(IUnitOfWork unitOfWork, Assignment assignment)
    {
        return await unitOfWork.Modules.GetByIdAsync(assignment.ModuleId);
    }

    public static bool IsUnlimitedAttempts(Module? module)
        => module != null && module.ModuleType == ModuleType.Theory && !module.IsDeleted;

    public static async Task<int> GetApprovedExtraAttemptsAsync(
        IUnitOfWork unitOfWork,
        Guid studentId,
        Guid assignmentId,
        Guid? moduleEnrollmentId)
    {
        var grants = await unitOfWork.AssessmentRecoveryRequests.GetAllAsync(
            r => r.StudentId == studentId
                 && r.AssignmentId == assignmentId
                 && r.Status == AssessmentRecoveryRequestStatus.Approved
                 && !r.IsDeleted
                 && (!moduleEnrollmentId.HasValue || r.ModuleEnrollmentId == moduleEnrollmentId.Value));

        return grants.Sum(r => r.ExtraAttemptsGranted);
    }

    public static async Task<int> GetEffectiveMaxAttemptsAsync(
        IUnitOfWork unitOfWork,
        Assignment assignment,
        Guid studentId,
        Guid? moduleEnrollmentId = null)
    {
        var module = await GetModuleForAssignmentAsync(unitOfWork, assignment);
        if (IsUnlimitedAttempts(module))
        {
            return int.MaxValue;
        }

        var extra = await GetApprovedExtraAttemptsAsync(
            unitOfWork,
            studentId,
            assignment.Id,
            moduleEnrollmentId);

        return assignment.MaxAttempts + extra;
    }

    /// <summary>
    /// Completed (Graded/TurnedIn) attempts for this assignment. When
    /// <paramref name="moduleEnrollmentId"/> is set, only that module enrollment
    /// counts — a rebuy ME starts from zero except for submissions copied onto it.
    /// </summary>
    public static async Task<int> CountCompletedAttemptsAsync(
        IUnitOfWork unitOfWork,
        Guid assignmentId,
        Guid studentId,
        Guid? moduleEnrollmentId = null)
    {
        var submissions = await unitOfWork.Submissions.GetAllAsync(
            s => s.AssignmentId == assignmentId
                 && s.StudentId == studentId
                 && !s.IsDeleted
                 && (s.Status == SubmissionStatus.Graded || s.Status == SubmissionStatus.TurnedIn)
                 && (!moduleEnrollmentId.HasValue || s.ModuleEnrollmentId == moduleEnrollmentId));

        return submissions.Count;
    }

    public static async Task<(DateTime? PersonalDueDate, DateTime? PersonalAvailableUntil)> GetPersonalWindowAsync(
        IUnitOfWork unitOfWork,
        Guid studentId,
        Guid assignmentId,
        Guid? moduleEnrollmentId)
    {
        var grants = await unitOfWork.AssessmentRecoveryRequests.GetAllAsync(
            r => r.StudentId == studentId
                 && r.AssignmentId == assignmentId
                 && r.Status == AssessmentRecoveryRequestStatus.Approved
                 && !r.IsDeleted
                 && (!moduleEnrollmentId.HasValue || r.ModuleEnrollmentId == moduleEnrollmentId.Value)
                 && (r.PersonalDueDate != null || r.PersonalAvailableUntil != null));

        if (grants.Count == 0)
        {
            return (null, null);
        }

        var latest = grants.OrderByDescending(r => r.DecidedAt ?? r.UpdatedAt).First();
        return (latest.PersonalDueDate, latest.PersonalAvailableUntil);
    }

    /// <summary>
    /// Effective availability end: personal override wins when later than assignment window.
    /// </summary>
    public static DateTime? ResolveEffectiveAvailableUntil(
        Assignment assignment,
        DateTime? personalAvailableUntil)
    {
        if (!personalAvailableUntil.HasValue)
        {
            return assignment.AvailableUntil;
        }

        if (!assignment.AvailableUntil.HasValue)
        {
            return personalAvailableUntil;
        }

        return personalAvailableUntil > assignment.AvailableUntil
            ? personalAvailableUntil
            : assignment.AvailableUntil;
    }

    public static DateTime? ResolveEffectiveDueDate(Assignment assignment, DateTime? personalDueDate)
    {
        if (!personalDueDate.HasValue)
        {
            return assignment.DueDate;
        }

        if (!assignment.DueDate.HasValue)
        {
            return personalDueDate;
        }

        return personalDueDate > assignment.DueDate ? personalDueDate : assignment.DueDate;
    }
}
