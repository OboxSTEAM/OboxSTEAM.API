using OboxSteam.Application.Utils;
using OboxSteam.Domain.Entities;
using OboxSteam.Domain.Enums;
using OboxSteam.Domain.Interfaces;

namespace OboxSteam.Application.Validation;

/// <summary>
/// Module enrollment business rules and input validation.
/// </summary>
public static class ModuleEnrollmentValidator
{
    public const string EnrollForbiddenMessage = "Only students can enroll in a module.";
    public const string ViewListForbiddenMessage = "You do not have permission to view module enrollments.";
    public const string ViewEnrollmentForbiddenMessage = "You do not have permission to view this enrollment.";

    public static void ValidateProgramEnrollmentIdRequired(Guid programEnrollmentId)
    {
        if (programEnrollmentId == Guid.Empty)
        {
            throw ErrorHelper.BadRequest("ProgramEnrollmentId is required.");
        }
    }

    public static void ValidateModuleIdRequired(Guid moduleId)
    {
        if (moduleId == Guid.Empty)
        {
            throw ErrorHelper.BadRequest("ModuleId is required.");
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
            throw ErrorHelper.BadRequest("Program enrollment must be active to enroll in a module.");
        }
    }

    public static void ValidateProgramEnrollmentActiveForRetake(ProgramEnrollment programEnrollment)
    {
        if (programEnrollment.Status != EnrollmentStatus.Active)
        {
            throw ErrorHelper.BadRequest("Program enrollment must be active to retake a module.");
        }
    }

    public static Module ValidateModuleExists(Module? module, Guid moduleId)
    {
        if (module == null || module.IsDeleted)
        {
            throw ErrorHelper.NotFound($"Module with id '{moduleId}' not found.");
        }

        return module;
    }

    public static void ValidateModuleBelongsToProgram(Module module, Guid programId)
    {
        if (module.ProgramId != programId)
        {
            throw ErrorHelper.BadRequest("Module does not belong to the enrolled program.");
        }
    }

    public static async Task ValidatePrerequisiteCompletedAsync(
        IUnitOfWork unitOfWork,
        Guid studentId,
        Module module)
    {
        if (!module.PrerequisiteModuleId.HasValue)
        {
            return;
        }

        var prerequisiteId = module.PrerequisiteModuleId.Value;

        var prerequisiteCompleted = await unitOfWork.ModuleEnrollments.FirstOrDefaultAsync(
            me => me.StudentId == studentId
                  && me.ModuleId == prerequisiteId
                  && me.Status == EnrollmentStatus.Completed
                  && !me.IsDeleted);

        if (prerequisiteCompleted == null)
        {
            throw ErrorHelper.BadRequest(
                $"Prerequisite module '{prerequisiteId}' must be completed before enrolling in this module.");
        }
    }

    public static void ValidateNoActiveEnrollment(ModuleEnrollment? activeEnrollment)
    {
        if (activeEnrollment != null)
        {
            throw ErrorHelper.Conflict("You already have an active enrollment for this module.");
        }
    }

    public static ModuleEnrollment ValidateRetakeEligibility(ModuleEnrollment? failedEnrollment)
    {
        if (failedEnrollment == null)
        {
            throw ErrorHelper.BadRequest(
                "Module retake is only allowed after failing the module (two failed assignment attempts).");
        }

        if (failedEnrollment.AssignmentFailureCount < 2)
        {
            throw ErrorHelper.BadRequest(
                "Module retake requires two failed assignment attempts on the previous attempt.");
        }

        return failedEnrollment;
    }

    public static ModuleEnrollment ValidateModuleEnrollmentExists(
        ModuleEnrollment? enrollment,
        Guid enrollmentId)
    {
        if (enrollment == null || enrollment.IsDeleted)
        {
            throw ErrorHelper.NotFound($"Module enrollment with id '{enrollmentId}' not found.");
        }

        return enrollment;
    }

    public static Guid ValidateProgramEnrollmentLink(Guid? programEnrollmentId)
    {
        if (!programEnrollmentId.HasValue)
        {
            throw ErrorHelper.BadRequest("Module enrollment is not linked to a program enrollment.");
        }

        return programEnrollmentId.Value;
    }
}
