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

    public static void ValidateProgramEnrollmentForCurriculum(ProgramEnrollment enrollment)
    {
        if (enrollment.Status is EnrollmentStatus.PendingPayment or EnrollmentStatus.Dropped)
        {
            throw ErrorHelper.Forbidden(EnrollmentNotActiveMessage);
        }
    }

    public static async Task<ProgramEnrollment> GetProgramEnrollmentForStudentActionAsync(
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

        ValidateProgramEnrollmentForCurriculum(enrollment);
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
}
