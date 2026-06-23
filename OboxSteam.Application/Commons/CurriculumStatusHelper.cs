using OboxSteam.Application.Utils;
using OboxSteam.Domain.Entities;
using OboxSteam.Domain.Enums;

namespace OboxSteam.Application.Commons;

/// <summary>
/// Derives enrollment-scoped activity nav states for the learn-page curriculum tree.
/// Sequential lock is within each course or research milestone; module prerequisite gates the whole module.
/// </summary>
public static class CurriculumStatusHelper
{
    public const string StatusCompleted = "completed";
    public const string StatusCurrent = "current";
    public const string StatusAvailable = "available";
    public const string StatusLocked = "locked";

    public static bool IsActivityCompleted(
        Guid activityId,
        Activity activity,
        IReadOnlyDictionary<Guid, ActivityProgress> progressByActivityId,
        IReadOnlySet<Guid> checkedInActivityIds)
    {
        if (checkedInActivityIds.Contains(activityId))
        {
            return true;
        }

        if (progressByActivityId.TryGetValue(activityId, out var progress)
            && progress.ActivityStatus == ActivityStatus.Done)
        {
            return true;
        }

        return false;
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
}
