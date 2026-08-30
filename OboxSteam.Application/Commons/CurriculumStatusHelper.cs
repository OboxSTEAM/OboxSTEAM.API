using OboxSteam.Application.Utils;
using OboxSteam.Application.Validation;
using OboxSteam.Domain.Entities;
using OboxSteam.Domain.Enums;

namespace OboxSteam.Application.Commons;

/// <summary>
/// Derives enrollment-scoped activity nav states for the learn-page curriculum tree.
/// Sequential lock is within each course or research milestone; module prerequisite gates the whole module.
/// LiveOnline/Offline attendance does not lock later activities or assignments.
/// </summary>
public static class CurriculumStatusHelper
{
    public const string StatusCompleted = "completed";
    public const string StatusCurrent = "current";
    public const string StatusAvailable = "available";
    public const string StatusLocked = "locked";
    public const string StatusSubmitted = "submitted";

    public static bool IsActivityCompleted(
        Guid activityId,
        IReadOnlyDictionary<Guid, ActivityProgress> progressByActivityId)
    {
        return progressByActivityId.TryGetValue(activityId, out var progress)
               && progress.ActivityStatus == ActivityStatus.Done;
    }

    public static bool IsModuleUnlocked(
        Module module,
        IReadOnlyDictionary<Guid, ModuleEnrollment> latestEnrollmentByModuleId,
        IReadOnlyDictionary<Guid, Module> modulesById)
    {
        if (!module.PrerequisiteModuleId.HasValue)
        {
            return true;
        }

        var prerequisiteId = module.PrerequisiteModuleId.Value;
        if (!latestEnrollmentByModuleId.TryGetValue(prerequisiteId, out var prerequisiteEnrollment))
        {
            return false;
        }

        return prerequisiteEnrollment.ProgressPercent >= 100m;
    }

    public static string? GetModuleLockReason(
        Module module,
        IReadOnlyDictionary<Guid, ModuleEnrollment> latestEnrollmentByModuleId,
        IReadOnlyDictionary<Guid, Module> modulesById)
    {
        if (IsModuleUnlocked(module, latestEnrollmentByModuleId, modulesById))
        {
            return null;
        }

        if (module.PrerequisiteModuleId.HasValue
            && modulesById.TryGetValue(module.PrerequisiteModuleId.Value, out var prerequisite))
        {
            return $"Complete module '{prerequisite.Name}' to unlock.";
        }

        return "Complete the prerequisite module to unlock.";
    }

    /// <summary>
    /// SelfPaced completion can lock later work. LiveOnline/Offline never do —
    /// absence is an attendance concern, not a homework gate. Unknown activities
    /// stay conservative and still gate.
    /// </summary>
    public static bool CompletionGatesUnlock(Activity? activity)
    {
        if (activity == null || activity.IsDeleted)
        {
            return true;
        }

        return activity.ActivityType == ActivityType.SelfPaced;
    }

    public static bool CompletionGatesUnlock(ProgramCurriculumTreeSnapshot snapshot, Guid activityId)
    {
        snapshot.ActivitiesById.TryGetValue(activityId, out var activity);
        return CompletionGatesUnlock(activity);
    }

    public static bool IsActivitySequentiallyAccessible(
        Guid activityId,
        ProgramCurriculumTreeSnapshot snapshot,
        Func<Guid, bool> isCompleted)
    {
        foreach (var orderedIds in snapshot.OrderedActivitiesByCourseId.Values)
        {
            var index = orderedIds.IndexOf(activityId);
            if (index < 0)
            {
                continue;
            }

            for (var i = 0; i < index; i++)
            {
                if (!CompletionGatesUnlock(snapshot, orderedIds[i]))
                {
                    continue;
                }

                if (!isCompleted(orderedIds[i]))
                {
                    return false;
                }
            }

            return true;
        }

        foreach (var orderedIds in snapshot.OrderedActivitiesByMilestoneId.Values)
        {
            var index = orderedIds.IndexOf(activityId);
            if (index < 0)
            {
                continue;
            }

            for (var i = 0; i < index; i++)
            {
                if (!CompletionGatesUnlock(snapshot, orderedIds[i]))
                {
                    continue;
                }

                if (!isCompleted(orderedIds[i]))
                {
                    return false;
                }
            }

            return true;
        }

        return snapshot.ActivityModuleMap.ContainsKey(activityId);
    }

    public static Guid? FindNextActivityId(
        ProgramCurriculumTreeSnapshot snapshot,
        Guid afterActivityId,
        Func<Guid, bool> isAccessible,
        Func<Guid, bool> isCompleted)
    {
        var startIndex = snapshot.GlobalActivityOrder.IndexOf(afterActivityId);
        if (startIndex < 0)
        {
            return null;
        }

        for (var i = startIndex + 1; i < snapshot.GlobalActivityOrder.Count; i++)
        {
            var candidateId = snapshot.GlobalActivityOrder[i];
            if (!isAccessible(candidateId) || isCompleted(candidateId))
            {
                continue;
            }

            return candidateId;
        }

        return null;
    }

    public static Guid? FindCurrentActivityId(
        ProgramCurriculumTreeSnapshot snapshot,
        Func<Guid, bool> isAccessible,
        Func<Guid, bool> isCompleted)
    {
        foreach (var activityId in snapshot.GlobalActivityOrder)
        {
            if (isAccessible(activityId) && !isCompleted(activityId))
            {
                return activityId;
            }
        }

        return null;
    }

    public static List<Guid> FindNewlyUnlockedModuleIds(
        ProgramCurriculumTreeSnapshot snapshot,
        Guid completedModuleId,
        IReadOnlyDictionary<Guid, ModuleEnrollment> latestEnrollmentByModuleId,
        IReadOnlyDictionary<Guid, Module> modulesById)
    {
        if (!latestEnrollmentByModuleId.TryGetValue(completedModuleId, out var completedEnrollment)
            || completedEnrollment.ProgressPercent < 100m)
        {
            return [];
        }

        return snapshot.Modules
            .Where(m => m.PrerequisiteModuleId == completedModuleId)
            .Where(m => IsModuleUnlocked(m, latestEnrollmentByModuleId, modulesById))
            .Select(m => m.Id)
            .ToList();
    }

    /// <summary>
    /// Course-scoped assignments unlock once every SelfPaced activity in the course is completed.
    /// Module-scoped assignments unlock once every SelfPaced activity in the module is completed.
    /// LiveOnline/Offline sessions do not gate unlock. Research milestone deliverables use
    /// <see cref="IsResearchMilestoneAssignmentAccessible"/>.
    /// </summary>
    public static bool IsAssignmentAccessible(
        Assignment assignment,
        Guid moduleId,
        ProgramCurriculumTreeSnapshot snapshot,
        Func<Guid, bool> isActivityCompleted,
        ResearchMilestone? researchMilestone = null,
        ResearchMilestone? previousResearchMilestone = null,
        IReadOnlyDictionary<Guid, Submission>? submissionsByMilestoneId = null)
    {
        if (researchMilestone != null)
        {
            return IsResearchMilestoneAssignmentAccessible(
                researchMilestone,
                previousResearchMilestone,
                snapshot,
                submissionsByMilestoneId ?? new Dictionary<Guid, Submission>(),
                isActivityCompleted);
        }

        if (assignment.CourseId.HasValue)
        {
            return AreAllCourseActivitiesCompleted(assignment.CourseId.Value, snapshot, isActivityCompleted);
        }

        return AreAllModuleActivitiesCompleted(moduleId, snapshot, isActivityCompleted);
    }

    public static bool AreAllCourseActivitiesCompleted(
        Guid courseId,
        ProgramCurriculumTreeSnapshot snapshot,
        Func<Guid, bool> isActivityCompleted)
    {
        if (!snapshot.OrderedActivitiesByCourseId.TryGetValue(courseId, out var activityIds)
            || activityIds.Count == 0)
        {
            return true;
        }

        return activityIds
            .Where(id => CompletionGatesUnlock(snapshot, id))
            .All(isActivityCompleted);
    }

    public static bool AreAllModuleActivitiesCompleted(
        Guid moduleId,
        ProgramCurriculumTreeSnapshot snapshot,
        Func<Guid, bool> isActivityCompleted)
    {
        var activityIds = snapshot.ActivityModuleMap
            .Where(kvp => kvp.Value == moduleId)
            .Select(kvp => kvp.Key)
            .ToList();

        if (activityIds.Count == 0)
        {
            return true;
        }

        return activityIds
            .Where(id => CompletionGatesUnlock(snapshot, id))
            .All(isActivityCompleted);
    }

    public static bool IsResearchMilestoneAssignmentAccessible(
        ResearchMilestone milestone,
        ResearchMilestone? previousMilestone,
        ProgramCurriculumTreeSnapshot snapshot,
        IReadOnlyDictionary<Guid, Submission> submissionsByMilestoneId,
        Func<Guid, bool> isActivityCompleted)
    {
        if (previousMilestone != null
            && !ResearchMilestoneValidator.HasPassedSubmission(
                previousMilestone,
                submissionsByMilestoneId,
                snapshot.AssignmentsById))
        {
            return false;
        }

        if (!snapshot.LinksByMilestoneId.TryGetValue(milestone.Id, out var links))
        {
            return true;
        }

        foreach (var link in links.Where(link => link.IsRequiredForSubmission))
        {
            if (!CompletionGatesUnlock(snapshot, link.ActivityId))
            {
                continue;
            }

            if (!isActivityCompleted(link.ActivityId))
            {
                return false;
            }
        }

        return true;
    }
}
