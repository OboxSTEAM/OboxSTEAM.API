using OboxSteam.Domain.Entities;
using OboxSteam.Domain.Enums;
using OboxSteam.Domain.Interfaces;

namespace OboxSteam.Application.Validation;

/// <summary>
/// Shared attempt rules for quiz / file / retrospective / research.
/// Theory modules: unlimited attempts. Other types: MaxAttempts + approved recovery grants.
/// Calendar open/close is the class AssignmentWindow (see AssignmentWindowPolicy).
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
}
