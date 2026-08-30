using Microsoft.Extensions.Logging;
using OboxSteam.Application.Validation;
using OboxSteam.Domain.Entities;
using OboxSteam.Domain.Enums;

namespace OboxSteam.Application.Services;

public partial class SeedService
{
    /// <summary>
    /// Top-up pass for ReadyForMentor/Open/InProgress classes: one session per missing
    /// LiveOnline/Offline activity (weekly pattern) and one AssignmentWindow work period
    /// per missing assignment.
    /// </summary>
    private async Task EnsureClassSessionCoverageAsync()
    {
        _loggerService.LogInformation("Ensuring class session coverage matches curriculum");

        var classes = await _unitOfWork.Classes.GetAllAsync(
            c => !c.IsDeleted
                 && (c.Status == ClassStatus.ReadyForMentor
                     || c.Status == ClassStatus.Open
                     || c.Status == ClassStatus.InProgress));

        if (classes.Count == 0)
        {
            return;
        }

        var sessionsToAdd = new List<ClassSession>();

        foreach (var classEntity in classes)
        {
            var definition = GetAcademicYearClassDefinitions()
                .FirstOrDefault(d => string.Equals(d.Code, classEntity.Code, StringComparison.OrdinalIgnoreCase));
            var weeklySlots = definition?.WeeklySlots
                ??
                [
                    new SeedTimeline.WeekdaySlot(DayOfWeek.Saturday, 9, 0, 90),
                    new SeedTimeline.WeekdaySlot(DayOfWeek.Sunday, 9, 0, 90),
                ];
            var fallbackMinutes = weeklySlots[0].DurationMinutes;

            var modules = (await _unitOfWork.Modules.GetAllAsync(
                    m => m.ProgramId == classEntity.ProgramId && !m.IsDeleted))
                .OrderBy(m => m.ModuleOrder)
                .ToList();
            if (modules.Count == 0)
            {
                continue;
            }

            var moduleIds = modules.Select(m => m.Id).ToList();
            var courses = await _unitOfWork.Courses.GetAllAsync(
                c => moduleIds.Contains(c.ModuleId) && !c.IsDeleted);
            var courseIds = courses.Select(c => c.Id).ToHashSet();
            var courseById = courses.ToDictionary(c => c.Id);

            var activities = await _unitOfWork.Activities.GetAllAsync(
                a => courseIds.Contains(a.CourseId)
                     && !a.IsDeleted
                     && (a.ActivityType == ActivityType.LiveOnline
                         || a.ActivityType == ActivityType.Offline));
            var assignments = await _unitOfWork.Assignments.GetAllAsync(
                a => moduleIds.Contains(a.ModuleId) && !a.IsDeleted);
            var existingSessions = await _unitOfWork.ClassSessions.GetAllAsync(
                s => s.ClassId == classEntity.Id
                     && !s.IsDeleted
                     && s.Status != ClassSessionStatus.Cancelled);

            var coveredActivityIds = existingSessions
                .Where(s => s.ActivityId.HasValue)
                .Select(s => s.ActivityId!.Value)
                .ToHashSet();
            var coveredAssignmentIds = existingSessions
                .Where(s => s.AssignmentId.HasValue)
                .Select(s => s.AssignmentId!.Value)
                .ToHashSet();

            var missingLives = new List<(Guid ModuleId, Activity Activity)>();
            foreach (var module in modules)
            {
                var moduleActivities = activities
                    .Where(a => courseById[a.CourseId].ModuleId == module.Id)
                    .OrderBy(a => courseById[a.CourseId].CourseOrder)
                    .ThenBy(a => a.ActivityOrder)
                    .ToList();

                missingLives.AddRange(moduleActivities
                    .Where(a => !coveredActivityIds.Contains(a.Id))
                    .Select(a => (module.Id, a)));
            }

            var nextLiveIndex = existingSessions.Count(s => s.ActivityId.HasValue);
            var addedLives = new List<ClassSession>();
            foreach (var (moduleId, activity) in missingLives)
            {
                var slot = SeedTimeline.TryResolveSlotSequence(
                    classEntity.StartDate,
                    classEntity.EndDate,
                    weeklySlots,
                    nextLiveIndex);
                nextLiveIndex++;
                if (slot == null)
                {
                    continue;
                }

                var durationMinutes = activity.DurationMinutes is > 0
                    ? activity.DurationMinutes.Value
                    : fallbackMinutes;
                var startTime = slot.Value.StartTime;
                var endTime = startTime.AddMinutes(durationMinutes);
                if (endTime.Date > classEntity.EndDate.Date)
                {
                    continue;
                }

                var sessionKind = ClassSessionValidator.ResolveSessionKind(activity, forAssignment: false);
                var (location, meetingUrl, latitude, longitude) = SeedTimeline.ResolveSeedVenue(
                    sessionKind,
                    classEntity.Code,
                    nextLiveIndex - 1);

                var live = new ClassSession
                {
                    Id = Guid.NewGuid(),
                    ClassId = classEntity.Id,
                    ModuleId = moduleId,
                    ActivityId = activity.Id,
                    SessionKind = sessionKind,
                    Title = activity.Name,
                    Description = activity.Description,
                    StartTime = startTime,
                    EndTime = endTime,
                    Location = location,
                    MeetingUrl = meetingUrl,
                    Latitude = latitude,
                    Longitude = longitude,
                    RequiresAttendance = true,
                    Status = SeedTimeline.ResolveSessionStatus(startTime, endTime, _seedNow),
                    CreatedAt = classEntity.CreatedAt,
                    CreatedBy = Guid.Empty,
                    IsDeleted = false,
                };
                addedLives.Add(live);
                sessionsToAdd.Add(live);
            }

            var activityById = activities.ToDictionary(a => a.Id);
            var allLives = existingSessions
                .Where(s => s.ActivityId.HasValue && activityById.ContainsKey(s.ActivityId.Value))
                .Concat(addedLives)
                .Select(s => new AssignmentWindowPlacement.ScheduledLive(
                    s.ActivityId!.Value,
                    s.ModuleId,
                    activityById[s.ActivityId.Value].CourseId,
                    s.StartTime,
                    s.EndTime))
                .ToList();

            var milestones = await _unitOfWork.ResearchMilestones.GetAllAsync(
                rm => moduleIds.Contains(rm.ModuleId) && !rm.IsDeleted);
            var milestoneIds = milestones.Select(m => m.Id).ToList();
            var milestoneLinks = milestoneIds.Count == 0
                ? []
                : await _unitOfWork.ResearchMilestoneActivities.GetAllAsync(
                    link => milestoneIds.Contains(link.ResearchMilestoneId) && !link.IsDeleted);

            var missingAssignments = assignments
                .Where(a => !coveredAssignmentIds.Contains(a.Id))
                .OrderBy(a => a.CreatedAt)
                .ThenBy(a => a.Code)
                .ToList();

            var coverageOrdinal = existingSessions.Count;
            foreach (var assignment in missingAssignments)
            {
                var milestoneLiveIds = AssignmentWindowPlacement.MilestoneLiveActivityIds(
                    assignment,
                    milestones,
                    milestoneLinks,
                    allLives);
                var open = AssignmentWindowPlacement.ResolveRelatedTeachingEnd(
                    classEntity.StartDate,
                    allLives,
                    assignment.ModuleId,
                    assignment.CourseId,
                    milestoneLiveIds);
                var nextLive = AssignmentWindowPlacement.NextLiveStart(allLives, open);
                if (!AssignmentWindowPlacement.TryComputeWindow(
                        open,
                        nextLive,
                        classEntity.EndDate,
                        out var close,
                        out _))
                {
                    continue;
                }

                sessionsToAdd.Add(CreateSeedAssignmentWindow(
                    classEntity,
                    assignment,
                    open,
                    close,
                    coverageOrdinal++));
            }
        }

        if (sessionsToAdd.Count == 0)
        {
            _loggerService.LogInformation("All Open/InProgress classes already have full session coverage.");
            return;
        }

        await _unitOfWork.ClassSessions.AddRangeAsync(sessionsToAdd);
        await _unitOfWork.SaveChangesAsync();
        _loggerService.LogInformation(
            "Class session coverage top-up — {Count} session(s) added across {ClassCount} class(es).",
            sessionsToAdd.Count,
            sessionsToAdd.Select(s => s.ClassId).Distinct().Count());
    }

    /// <summary>
    /// Places or refreshes AssignmentWindow work periods after lives (and weekly realign)
    /// are final, including fail/rebuy and Completed classes.
    /// </summary>
    private async Task EnsureAssignmentWorkWindowsAsync()
    {
        _loggerService.LogInformation("Ensuring AssignmentWindow work periods after related teaching");

        var classes = await _unitOfWork.Classes.GetAllAsync(
            c => !c.IsDeleted
                 && (c.Status == ClassStatus.ReadyForMentor
                     || c.Status == ClassStatus.Open
                     || c.Status == ClassStatus.InProgress
                     || c.Status == ClassStatus.Completed));
        if (classes.Count == 0)
        {
            return;
        }

        var created = 0;
        var updated = 0;

        foreach (var classEntity in classes)
        {
            var modules = (await _unitOfWork.Modules.GetAllAsync(
                    m => m.ProgramId == classEntity.ProgramId && !m.IsDeleted))
                .OrderBy(m => m.ModuleOrder)
                .ToList();
            if (modules.Count == 0)
            {
                continue;
            }

            var moduleIds = modules.Select(m => m.Id).ToList();
            var courses = await _unitOfWork.Courses.GetAllAsync(
                c => moduleIds.Contains(c.ModuleId) && !c.IsDeleted);
            var courseIds = courses.Select(c => c.Id).ToHashSet();
            var activities = await _unitOfWork.Activities.GetAllAsync(
                a => courseIds.Contains(a.CourseId)
                     && !a.IsDeleted
                     && (a.ActivityType == ActivityType.LiveOnline
                         || a.ActivityType == ActivityType.Offline));
            var activityById = activities.ToDictionary(a => a.Id);
            var assignments = (await _unitOfWork.Assignments.GetAllAsync(
                    a => moduleIds.Contains(a.ModuleId) && !a.IsDeleted))
                .OrderBy(a => a.CreatedAt)
                .ThenBy(a => a.Code)
                .ToList();
            if (assignments.Count == 0)
            {
                continue;
            }

            var sessions = await _unitOfWork.ClassSessions.GetAllAsync(
                s => s.ClassId == classEntity.Id
                     && !s.IsDeleted
                     && s.Status != ClassSessionStatus.Cancelled);
            var lives = sessions
                .Where(s => s.ActivityId.HasValue
                            && activityById.ContainsKey(s.ActivityId.Value)
                            && s.SessionKind is SessionKind.LiveOnline or SessionKind.Offline)
                .OrderBy(s => s.StartTime)
                .Select(s => new AssignmentWindowPlacement.ScheduledLive(
                    s.ActivityId!.Value,
                    s.ModuleId,
                    activityById[s.ActivityId.Value].CourseId,
                    s.StartTime,
                    s.EndTime))
                .ToList();

            var milestones = await _unitOfWork.ResearchMilestones.GetAllAsync(
                rm => moduleIds.Contains(rm.ModuleId) && !rm.IsDeleted);
            var milestoneIds = milestones.Select(m => m.Id).ToList();
            var milestoneLinks = milestoneIds.Count == 0
                ? []
                : await _unitOfWork.ResearchMilestoneActivities.GetAllAsync(
                    link => milestoneIds.Contains(link.ResearchMilestoneId) && !link.IsDeleted);

            var windowsByAssignmentId = sessions
                .Where(s => s.SessionKind == SessionKind.AssignmentWindow && s.AssignmentId.HasValue)
                .GroupBy(s => s.AssignmentId!.Value)
                .ToDictionary(g => g.Key, g => g.OrderBy(s => s.StartTime).First());

            var ordinal = sessions.Count;
            var toAdd = new List<ClassSession>();
            foreach (var assignment in assignments)
            {
                var milestoneLiveIds = AssignmentWindowPlacement.MilestoneLiveActivityIds(
                    assignment,
                    milestones,
                    milestoneLinks,
                    lives);
                var open = AssignmentWindowPlacement.ResolveRelatedTeachingEnd(
                    classEntity.StartDate,
                    lives,
                    assignment.ModuleId,
                    assignment.CourseId,
                    milestoneLiveIds);
                var nextLive = AssignmentWindowPlacement.NextLiveStart(lives, open);
                if (!AssignmentWindowPlacement.TryComputeWindow(
                        open,
                        nextLive,
                        classEntity.EndDate,
                        out var close,
                        out _))
                {
                    continue;
                }

                var status = SeedTimeline.ResolveSessionStatus(open, close, _seedNow);
                if (windowsByAssignmentId.TryGetValue(assignment.Id, out var existing))
                {
                    if (existing.StartTime == open
                        && existing.EndTime == close
                        && existing.Status == status
                        && !existing.RequiresAttendance)
                    {
                        continue;
                    }

                    existing.StartTime = open;
                    existing.EndTime = close;
                    existing.Status = status;
                    existing.RequiresAttendance = false;
                    existing.UpdatedAt = _seedNow;
                    existing.UpdatedBy = Guid.Empty;
                    await _unitOfWork.ClassSessions.Update(existing);
                    updated++;
                    continue;
                }

                toAdd.Add(CreateSeedAssignmentWindow(classEntity, assignment, open, close, ordinal++));
                created++;
            }

            if (toAdd.Count > 0)
            {
                await _unitOfWork.ClassSessions.AddRangeAsync(toAdd);
            }
        }

        if (created == 0 && updated == 0)
        {
            _loggerService.LogInformation("AssignmentWindow work periods already match related teaching.");
            return;
        }

        await _unitOfWork.SaveChangesAsync();
        _loggerService.LogInformation(
            "AssignmentWindow work periods — {Created} created, {Updated} updated.",
            created,
            updated);
    }
}
