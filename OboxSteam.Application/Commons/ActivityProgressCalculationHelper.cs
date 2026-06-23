using OboxSteam.Application.Utils;
using OboxSteam.Application.Validation;
using OboxSteam.Domain.Entities;
using OboxSteam.Domain.Enums;
using OboxSteam.Domain.Interfaces;

namespace OboxSteam.Application.Commons;

/// <summary>
/// Recalculates module and program enrollment progress after activity completion.
/// </summary>
public static class ActivityProgressCalculationHelper
{
    public static async Task<decimal> RecalculateModuleProgressAsync(
        IUnitOfWork unitOfWork,
        ModuleEnrollment moduleEnrollment)
    {
        var activityIds = await GetModuleActivityIdsAsync(
            unitOfWork,
            moduleEnrollment.ModuleId);
        var totalActivities = activityIds.Count;

        if (totalActivities == 0)
        {
            moduleEnrollment.ProgressPercent = 0m;
            await unitOfWork.ModuleEnrollments.Update(moduleEnrollment);
            return 0m;
        }

        var doneProgresses = await unitOfWork.ActivityProgresses.GetAllAsync(
            ap => ap.ModuleEnrollmentId == moduleEnrollment.Id
                  && ap.ActivityStatus == ActivityStatus.Done
                  && !ap.IsDeleted);

        var doneCount = doneProgresses.Count;
        var progressPercent = Math.Round((decimal)doneCount / totalActivities * 100m, 2);

        moduleEnrollment.ProgressPercent = progressPercent;

        if (progressPercent >= 100m)
        {
            moduleEnrollment.Status = EnrollmentStatus.Completed;
            moduleEnrollment.CompletedAt ??= DateTime.UtcNow;
        }

        await unitOfWork.ModuleEnrollments.Update(moduleEnrollment);

        return progressPercent;
    }

    public static async Task<List<Guid>> GetModuleActivityIdsAsync(IUnitOfWork unitOfWork, Guid moduleId)
    {
        var module = await unitOfWork.Modules.GetByIdAsync(moduleId);
        var activityIds = await ActivityProgressValidator.GetModuleActivityIdsAsync(unitOfWork, moduleId);

        if (module?.ModuleType != ModuleType.Research)
        {
            return activityIds;
        }

        var milestones = await unitOfWork.ResearchMilestones.GetAllAsync(
            rm => rm.ModuleId == moduleId && !rm.IsDeleted);

        if (milestones.Count == 0)
        {
            return activityIds;
        }

        var milestoneIds = milestones.Select(m => m.Id).ToList();
        var links = await unitOfWork.ResearchMilestoneActivities.GetAllAsync(
            rma => milestoneIds.Contains(rma.ResearchMilestoneId) && !rma.IsDeleted);

        var researchActivityIds = links.Select(l => l.ActivityId).Distinct();
        return activityIds.Concat(researchActivityIds).Distinct().ToList();
    }

    public static async Task<decimal> RecalculateProgramProgressAsync(
        IUnitOfWork unitOfWork,
        Guid programEnrollmentId,
        ModuleEnrollment updatedModuleEnrollment)
    {
        var programEnrollmentEntity = await unitOfWork.ProgramEnrollments.GetByIdAsync(programEnrollmentId);
        if (programEnrollmentEntity == null || programEnrollmentEntity.IsDeleted)
        {
            throw ErrorHelper.NotFound($"Program enrollment with id '{programEnrollmentId}' not found.");
        }

        var modules = await unitOfWork.Modules.GetAllAsync(
            m => m.ProgramId == programEnrollmentEntity.ProgramId && !m.IsDeleted);

        var totalModules = modules.Count;
        if (totalModules == 0)
        {
            programEnrollmentEntity.ProgressPercent = 0m;
            await unitOfWork.ProgramEnrollments.Update(programEnrollmentEntity);
            return 0m;
        }

        var moduleEnrollments = await unitOfWork.ModuleEnrollments.GetAllAsync(
            me => me.ProgramEnrollmentId == programEnrollmentId
                  && me.StudentId == programEnrollmentEntity.StudentId
                  && !me.IsDeleted);

        var latestEnrollmentByModule = moduleEnrollments
            .GroupBy(me => me.ModuleId)
            .ToDictionary(
                g => g.Key,
                g => g.OrderByDescending(me => me.AttemptNumber).First());

        latestEnrollmentByModule[updatedModuleEnrollment.ModuleId] = updatedModuleEnrollment;

        var completedModules = modules.Count(module =>
            latestEnrollmentByModule.TryGetValue(module.Id, out var enrollment)
            && enrollment.ProgressPercent >= 100m);

        var progressPercent = Math.Round((decimal)completedModules / totalModules * 100m, 2);

        programEnrollmentEntity.ProgressPercent = progressPercent;
        await unitOfWork.ProgramEnrollments.Update(programEnrollmentEntity);

        return progressPercent;
    }
}
