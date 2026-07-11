using OboxSteam.Application.Utils;
using OboxSteam.Application.Validation;
using OboxSteam.Domain.Entities;
using OboxSteam.Domain.Enums;
using OboxSteam.Domain.Interfaces;

namespace OboxSteam.Application.Commons;

/// <summary>
/// Recalculates module and program enrollment progress after activity or assignment changes.
/// Progress is measured in "units": each activity is one unit and each required assignment
/// (<see cref="Assignment.IsRequiredForModulePass"/>) is one unit.
/// Module progress: (done activities + passed required assignments) / total module units.
/// Program progress: the same ratio aggregated across every module in the program.
/// An assignment counts as done when it has a graded submission whose grade meets the
/// assignment's <see cref="Assignment.PassScore"/> for that module enrollment attempt.
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
        var requiredAssignments = await GetRequiredModuleAssignmentsAsync(
            unitOfWork,
            moduleEnrollment.ModuleId);

        var totalUnits = activityIds.Count + requiredAssignments.Count;

        if (totalUnits == 0)
        {
            moduleEnrollment.ProgressPercent = 0m;
            await unitOfWork.ModuleEnrollments.Update(moduleEnrollment);
            return 0m;
        }

        var doneActivities = await CountDoneActivitiesAsync(unitOfWork, moduleEnrollment.Id);
        var passedAssignments = await CountPassedAssignmentsAsync(
            unitOfWork,
            moduleEnrollment.Id,
            requiredAssignments);

        var doneUnits = doneActivities + passedAssignments;
        var progressPercent = Math.Round((decimal)doneUnits / totalUnits * 100m, 2);

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

    /// <summary>
    /// Returns the assignments in a module that must be passed for the module to complete
    /// (covers course-scoped, module-scoped, and research-milestone assignments, since every
    /// assignment carries the owning <see cref="Assignment.ModuleId"/>).
    /// </summary>
    public static async Task<List<Assignment>> GetRequiredModuleAssignmentsAsync(
        IUnitOfWork unitOfWork,
        Guid moduleId)
    {
        var assignments = await unitOfWork.Assignments.GetAllAsync(
            a => a.ModuleId == moduleId
                 && a.IsRequiredForModulePass
                 && !a.IsDeleted);

        return assignments;
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

        if (modules.Count == 0)
        {
            programEnrollmentEntity.ProgressPercent = 0m;
            await unitOfWork.ProgramEnrollments.Update(programEnrollmentEntity);
            return 0m;
        }

        var moduleEnrollments = await unitOfWork.ModuleEnrollments.GetAllAsync(
            me => me.ProgramEnrollmentId == programEnrollmentId
                  && me.StudentId == programEnrollmentEntity.StudentId
                  && !me.IsDeleted);

        var latestEnrollmentByModuleId = moduleEnrollments
            .GroupBy(me => me.ModuleId)
            .ToDictionary(
                g => g.Key,
                g => g.OrderByDescending(me => me.AttemptNumber).First());

        latestEnrollmentByModuleId[updatedModuleEnrollment.ModuleId] = updatedModuleEnrollment;

        var totalUnits = 0;
        var doneUnits = 0;

        foreach (var module in modules)
        {
            var activityIds = await GetModuleActivityIdsAsync(unitOfWork, module.Id);
            var requiredAssignments = await GetRequiredModuleAssignmentsAsync(unitOfWork, module.Id);
            totalUnits += activityIds.Count + requiredAssignments.Count;

            if (!latestEnrollmentByModuleId.TryGetValue(module.Id, out var moduleEnrollment))
            {
                continue;
            }

            var doneActivities = await CountDoneActivitiesAsync(unitOfWork, moduleEnrollment.Id);
            var passedAssignments = await CountPassedAssignmentsAsync(
                unitOfWork,
                moduleEnrollment.Id,
                requiredAssignments);

            doneUnits += doneActivities + passedAssignments;
        }

        if (totalUnits == 0)
        {
            programEnrollmentEntity.ProgressPercent = 0m;
            await unitOfWork.ProgramEnrollments.Update(programEnrollmentEntity);
            return 0m;
        }

        var progressPercent = Math.Round((decimal)doneUnits / totalUnits * 100m, 2);

        programEnrollmentEntity.ProgressPercent = progressPercent;
        await unitOfWork.ProgramEnrollments.Update(programEnrollmentEntity);

        return progressPercent;
    }

    private static async Task<int> CountDoneActivitiesAsync(IUnitOfWork unitOfWork, Guid moduleEnrollmentId)
    {
        var doneProgresses = await unitOfWork.ActivityProgresses.GetAllAsync(
            ap => ap.ModuleEnrollmentId == moduleEnrollmentId
                  && ap.ActivityStatus == ActivityStatus.Done
                  && !ap.IsDeleted);

        return doneProgresses.Count;
    }

    /// <summary>
    /// Counts how many of the given required assignments have a passing graded submission
    /// under this module enrollment attempt (grade meets the assignment's PassScore).
    /// </summary>
    private static async Task<int> CountPassedAssignmentsAsync(
        IUnitOfWork unitOfWork,
        Guid moduleEnrollmentId,
        IReadOnlyList<Assignment> requiredAssignments)
    {
        if (requiredAssignments.Count == 0)
        {
            return 0;
        }

        var assignmentById = requiredAssignments.ToDictionary(a => a.Id);
        var assignmentIds = assignmentById.Keys.ToList();

        var gradedSubmissions = await unitOfWork.Submissions.GetAllAsync(
            s => s.ModuleEnrollmentId == moduleEnrollmentId
                 && assignmentIds.Contains(s.AssignmentId)
                 && s.Status == SubmissionStatus.Graded
                 && s.AssignedGrade != null
                 && !s.IsDeleted);

        return gradedSubmissions
            .Where(s => assignmentById.TryGetValue(s.AssignmentId, out var assignment)
                        && s.AssignedGrade!.Value >= assignment.PassScore)
            .Select(s => s.AssignmentId)
            .Distinct()
            .Count();
    }
}
