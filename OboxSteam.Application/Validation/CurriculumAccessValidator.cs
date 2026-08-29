using OboxSteam.Application.Utils;
using OboxSteam.Domain.Entities;
using OboxSteam.Domain.Enums;
using OboxSteam.Domain.Interfaces;

namespace OboxSteam.Application.Validation;

/// <summary>
/// Enrollment-scoped curriculum access rules for learn-page reads and activity completion.
/// </summary>
public static class CurriculumAccessValidator
{
    public const string CurriculumForbiddenMessage = "You do not have access to this program enrollment curriculum.";
    public const string ActivityLockedMessage = "This activity is locked until prerequisites are met.";
    public const string EnrollmentNotActiveMessage = "Program enrollment must be active to access curriculum.";
    public const string SelfPacedOnlyMessage = "Only SelfPaced activities can be marked complete via this endpoint.";
    public const string MentorSessionOnlyMessage =
        "Only LiveOnline or Offline session activities can be mentor-completed.";

    /// <summary>
    /// Read access: any enrollment except <see cref="EnrollmentStatus.PendingPayment"/> (unpaid).
    /// Failed/Dropped/Completed keep read-only curriculum access.
    /// </summary>
    public static void ValidateProgramEnrollmentForCurriculum(ProgramEnrollment enrollment)
    {
        if (enrollment.Status is EnrollmentStatus.PendingPayment)
        {
            throw ErrorHelper.Forbidden(EnrollmentNotActiveMessage);
        }
    }

    /// <summary>
    /// Mutation access: only <see cref="EnrollmentStatus.Active"/> enrollments may change
    /// learning data (complete activities, save checkpoints).
    /// </summary>
    public static void ValidateProgramEnrollmentForCurriculumMutation(ProgramEnrollment enrollment)
    {
        if (enrollment.Status != EnrollmentStatus.Active)
        {
            throw ErrorHelper.Forbidden(EnrollmentNotActiveMessage);
        }
    }

    public static async Task<ProgramEnrollment> GetProgramEnrollmentForStudentActionAsync(
        IUnitOfWork unitOfWork,
        Guid programEnrollmentId,
        Guid studentId)
    {
        var enrollment = await GetOwnedEnrollmentAsync(unitOfWork, programEnrollmentId, studentId);
        ValidateProgramEnrollmentForCurriculum(enrollment);
        return enrollment;
    }

    public static async Task<ProgramEnrollment> GetProgramEnrollmentForStudentMutationAsync(
        IUnitOfWork unitOfWork,
        Guid programEnrollmentId,
        Guid studentId)
    {
        var enrollment = await GetOwnedEnrollmentAsync(unitOfWork, programEnrollmentId, studentId);
        ValidateProgramEnrollmentForCurriculumMutation(enrollment);
        return enrollment;
    }

    private static async Task<ProgramEnrollment> GetOwnedEnrollmentAsync(
        IUnitOfWork unitOfWork,
        Guid programEnrollmentId,
        Guid studentId)
    {
        var enrollment = await unitOfWork.ProgramEnrollments.GetByIdAsync(programEnrollmentId);
        if (enrollment == null || enrollment.IsDeleted)
        {
            throw ErrorHelper.NotFound($"Program enrollment with id '{programEnrollmentId}' not found.");
        }

        if (enrollment.StudentId != studentId)
        {
            throw ErrorHelper.Forbidden(CurriculumForbiddenMessage);
        }

        return enrollment;
    }

    public static async Task<ModuleEnrollment> ResolveModuleEnrollmentAsync(
        IUnitOfWork unitOfWork,
        Guid programEnrollmentId,
        Guid studentId,
        Guid moduleId)
    {
        var moduleEnrollments = await unitOfWork.ModuleEnrollments.GetAllAsync(
            me => me.ProgramEnrollmentId == programEnrollmentId
                  && me.StudentId == studentId
                  && me.ModuleId == moduleId
                  && !me.IsDeleted);

        var latest = moduleEnrollments
            .OrderByDescending(me => me.AttemptNumber)
            .FirstOrDefault();

        if (latest == null)
        {
            throw ErrorHelper.NotFound($"Module enrollment for module '{moduleId}' not found.");
        }

        return latest;
    }

    public static void ValidateActivityTypeForManualComplete(Activity activity)
    {
        if (activity.ActivityType != ActivityType.SelfPaced)
        {
            throw ErrorHelper.BadRequest(SelfPacedOnlyMessage);
        }
    }

    public static void ValidateActivityTypeForMentorComplete(Activity activity)
    {
        if (activity.ActivityType is not (ActivityType.LiveOnline or ActivityType.Offline))
        {
            throw ErrorHelper.BadRequest(MentorSessionOnlyMessage);
        }
    }
}
