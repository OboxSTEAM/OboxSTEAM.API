using OboxSteam.Domain.Entities;
using OboxSteam.Domain.Enums;

namespace OboxSteam.Application.Commons;

/// <summary>
/// Class-scoped nav statuses for the mentor curriculum tree (not student lock semantics).
/// Hybrid: LiveOnline/Offline follow the linked class session; SelfPaced uses roster Done
/// or “class moved past” when a later activity is current/completed. Exactly one activity
/// may be <c>current</c>.
/// </summary>
public static class ClassCurriculumNavStatusHelper
{
    public sealed record ActivityNavInput(
        Guid ActivityId,
        ActivityType ActivityType,
        int CompletedCount,
        ClassSession? PrimarySession);

    public sealed record ActivityNavResult(
        Guid ActivityId,
        string Status,
        Guid? ClassSessionId,
        ClassSessionStatus? SessionStatus);

    /// <summary>
    /// Resolves class nav statuses for activities in program order.
    /// </summary>
    public static (IReadOnlyDictionary<Guid, ActivityNavResult> ByActivityId, Guid? CurrentActivityId)
        ResolveActivityStatuses(IReadOnlyList<ActivityNavInput> orderedActivities, int totalStudents)
    {
        if (orderedActivities.Count == 0)
        {
            return (new Dictionary<Guid, ActivityNavResult>(), null);
        }

        var hardCompleted = new HashSet<Guid>();
        foreach (var activity in orderedActivities)
        {
            if (IsHardCompleted(activity, totalStudents))
            {
                hardCompleted.Add(activity.ActivityId);
            }
        }

        var currentId = FindPreferredCurrent(orderedActivities, hardCompleted);

        var statuses = new Dictionary<Guid, string>(orderedActivities.Count);
        foreach (var activity in orderedActivities)
        {
            if (hardCompleted.Contains(activity.ActivityId))
            {
                statuses[activity.ActivityId] = CurriculumStatusHelper.StatusCompleted;
            }
            else if (currentId == activity.ActivityId)
            {
                statuses[activity.ActivityId] = CurriculumStatusHelper.StatusCurrent;
            }
            else
            {
                statuses[activity.ActivityId] = CurriculumStatusHelper.StatusAvailable;
            }
        }

        ApplySelfPacedMovedPast(orderedActivities, statuses);

        if (currentId.HasValue
            && statuses.TryGetValue(currentId.Value, out var currentStatus)
            && currentStatus == CurriculumStatusHelper.StatusCompleted)
        {
            currentId = orderedActivities
                .Select(a => a.ActivityId)
                .FirstOrDefault(id => statuses[id] != CurriculumStatusHelper.StatusCompleted);

            if (currentId == Guid.Empty)
            {
                currentId = null;
            }

            foreach (var activity in orderedActivities)
            {
                if (statuses[activity.ActivityId] == CurriculumStatusHelper.StatusCompleted)
                {
                    continue;
                }

                statuses[activity.ActivityId] = currentId == activity.ActivityId
                    ? CurriculumStatusHelper.StatusCurrent
                    : CurriculumStatusHelper.StatusAvailable;
            }
        }

        var byId = orderedActivities.ToDictionary(
            a => a.ActivityId,
            a => new ActivityNavResult(
                a.ActivityId,
                statuses[a.ActivityId],
                a.PrimarySession?.Id,
                a.PrimarySession?.Status));

        return (byId, currentId);
    }

    /// <summary>
    /// Class-scoped assignment nav: completed when every active student is graded;
    /// submitted when any handed-in work still awaits grading; otherwise available.
    /// </summary>
    public static string ResolveAssignmentStatus(int totalStudents, int submittedCount, int gradedCount)
    {
        if (totalStudents > 0 && gradedCount >= totalStudents)
        {
            return CurriculumStatusHelper.StatusCompleted;
        }

        if (submittedCount > gradedCount)
        {
            return CurriculumStatusHelper.StatusSubmitted;
        }

        return CurriculumStatusHelper.StatusAvailable;
    }

    /// <summary>
    /// Picks the primary session for Live/Offline status: ignore Cancelled;
    /// prefer Completed, then InProgress, then earliest StartTime.
    /// </summary>
    public static ClassSession? SelectPrimarySession(IEnumerable<ClassSession> sessions)
    {
        return sessions
            .Where(s => !s.IsDeleted && s.Status != ClassSessionStatus.Cancelled)
            .OrderBy(s => s.Status switch
            {
                ClassSessionStatus.Completed => 0,
                ClassSessionStatus.InProgress => 1,
                ClassSessionStatus.Scheduled => 2,
                _ => 3,
            })
            .ThenBy(s => s.StartTime)
            .FirstOrDefault();
    }

    private static bool IsHardCompleted(ActivityNavInput activity, int totalStudents)
    {
        if (activity.ActivityType is ActivityType.LiveOnline or ActivityType.Offline)
        {
            if (activity.PrimarySession?.Status == ClassSessionStatus.Completed)
            {
                return true;
            }

            // Mentor-complete-bulk applied to the full active roster (no absentees left undone).
            return totalStudents > 0 && activity.CompletedCount >= totalStudents;
        }

        // SelfPaced: all active students Done. Vacuous true with empty roster is not "completed".
        return totalStudents > 0 && activity.CompletedCount >= totalStudents;
    }

    private static Guid? FindPreferredCurrent(
        IReadOnlyList<ActivityNavInput> orderedActivities,
        IReadOnlySet<Guid> hardCompleted)
    {
        var inProgressLive = orderedActivities.FirstOrDefault(a =>
            !hardCompleted.Contains(a.ActivityId)
            && a.ActivityType is ActivityType.LiveOnline or ActivityType.Offline
            && a.PrimarySession?.Status == ClassSessionStatus.InProgress);

        if (inProgressLive != null)
        {
            return inProgressLive.ActivityId;
        }

        var firstIncomplete = orderedActivities
            .FirstOrDefault(a => !hardCompleted.Contains(a.ActivityId));

        return firstIncomplete?.ActivityId;
    }

    private static void ApplySelfPacedMovedPast(
        IReadOnlyList<ActivityNavInput> orderedActivities,
        Dictionary<Guid, string> statuses)
    {
        for (var i = 0; i < orderedActivities.Count; i++)
        {
            var activity = orderedActivities[i];
            if (activity.ActivityType != ActivityType.SelfPaced)
            {
                continue;
            }

            if (statuses[activity.ActivityId] == CurriculumStatusHelper.StatusCompleted)
            {
                continue;
            }

            var laterAdvanced = orderedActivities
                .Skip(i + 1)
                .Any(later =>
                {
                    var laterStatus = statuses[later.ActivityId];
                    return laterStatus is CurriculumStatusHelper.StatusCurrent
                        or CurriculumStatusHelper.StatusCompleted;
                });

            if (laterAdvanced)
            {
                statuses[activity.ActivityId] = CurriculumStatusHelper.StatusCompleted;
            }
        }
    }
}
