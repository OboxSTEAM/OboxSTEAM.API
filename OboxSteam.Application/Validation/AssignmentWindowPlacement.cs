using OboxSteam.Domain.Entities;

namespace OboxSteam.Application.Validation;

/// <summary>
/// Places AssignmentWindow as a work period after related teaching and before the next live.
/// </summary>
public static class AssignmentWindowPlacement
{
    public const int MinimumWindowHours = 48;

    public sealed record ScheduledLive(
        Guid ActivityId,
        Guid ModuleId,
        Guid CourseId,
        DateTime StartTime,
        DateTime EndTime);

    public static DateTime EndOfClassDay(DateTime classEndDate)
    {
        var date = DateTime.SpecifyKind(classEndDate.Date, DateTimeKind.Utc);
        return date.AddDays(1).AddTicks(-1);
    }

    public static DateTime ResolveRelatedTeachingEnd(
        DateTime classStartDate,
        IReadOnlyList<ScheduledLive> lives,
        Guid moduleId,
        Guid? courseId,
        IReadOnlyCollection<Guid>? milestoneLiveActivityIds)
    {
        if (milestoneLiveActivityIds is { Count: > 0 })
        {
            var set = milestoneLiveActivityIds.ToHashSet();
            var milestoneLives = lives.Where(live => set.Contains(live.ActivityId)).ToList();
            if (milestoneLives.Count > 0)
            {
                return milestoneLives.Max(live => live.EndTime);
            }
        }

        if (courseId.HasValue)
        {
            var courseLives = lives.Where(live => live.CourseId == courseId.Value).ToList();
            if (courseLives.Count > 0)
            {
                return courseLives.Max(live => live.EndTime);
            }
        }

        var moduleLives = lives.Where(live => live.ModuleId == moduleId).ToList();
        if (moduleLives.Count > 0)
        {
            return moduleLives.Max(live => live.EndTime);
        }

        var start = classStartDate.Kind == DateTimeKind.Unspecified
            ? DateTime.SpecifyKind(classStartDate, DateTimeKind.Utc)
            : classStartDate;
        return start;
    }

    public static DateTime? NextLiveStart(IReadOnlyList<ScheduledLive> lives, DateTime afterExclusive)
        => lives
            .Where(live => live.StartTime > afterExclusive)
            .OrderBy(live => live.StartTime)
            .Select(live => (DateTime?)live.StartTime)
            .FirstOrDefault();

    public static bool TryComputeWindow(
        DateTime open,
        DateTime? nextLiveStart,
        DateTime classEndDate,
        out DateTime close,
        out string? error)
    {
        error = null;
        var classEnd = EndOfClassDay(classEndDate);
        close = nextLiveStart ?? classEnd;
        var minClose = open.AddHours(MinimumWindowHours);
        if (close < minClose)
        {
            close = minClose <= classEnd ? minClose : classEnd;
        }

        if (close <= open)
        {
            error =
                $"Assignment window would close at {close:yyyy-MM-dd HH:mm} UTC, which is not after open " +
                $"{open:yyyy-MM-dd HH:mm} UTC. Extend the class end date so work windows have at least " +
                $"{MinimumWindowHours} hours.";
            return false;
        }

        return true;
    }

    public static IReadOnlyCollection<Guid>? MilestoneLiveActivityIds(
        Assignment assignment,
        IReadOnlyList<ResearchMilestone> milestones,
        IReadOnlyList<ResearchMilestoneActivity> links,
        IReadOnlyList<ScheduledLive> lives)
    {
        var milestone = milestones.FirstOrDefault(m => m.AssignmentId == assignment.Id && !m.IsDeleted);
        if (milestone == null)
        {
            return null;
        }

        var liveIds = lives.Select(live => live.ActivityId).ToHashSet();
        var ids = links
            .Where(link => link.ResearchMilestoneId == milestone.Id && !link.IsDeleted)
            .Select(link => link.ActivityId)
            .Where(liveIds.Contains)
            .ToList();

        return ids.Count == 0 ? null : ids;
    }
}
