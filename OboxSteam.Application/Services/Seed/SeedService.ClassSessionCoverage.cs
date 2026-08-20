using Microsoft.Extensions.Logging;
using OboxSteam.Domain.Entities;
using OboxSteam.Domain.Enums;

namespace OboxSteam.Application.Services;

public partial class SeedService
{
    /// <summary>
    /// Top-up pass that keeps seeded classes compliant with the scheduling invariants:
    /// an Open/InProgress class must have exactly one active session per schedulable
    /// curriculum item (every LiveOnline/Offline activity plus every assignment, walked in
    /// module → course → activity order). Template-based session seeds only cover named
    /// activities, so this pass fills the gaps (assignments, programs without templates).
    /// Idempotent: items that already have an active session are skipped, which also
    /// repairs databases seeded before the coverage rule existed.
    /// </summary>
    private async Task EnsureClassSessionCoverageAsync()
    {
        _loggerService.LogInformation("Ensuring class session coverage matches curriculum");

        var classes = await _unitOfWork.Classes.GetAllAsync(
            c => !c.IsDeleted
                 && c.Status != ClassStatus.Completed
                 && c.Status != ClassStatus.Cancelled);

        if (classes.Count == 0)
        {
            return;
        }

        var seedTime = DateTime.UtcNow;
        var sessionsToAdd = new List<ClassSession>();
        var classesToExtend = new List<Class>();

        foreach (var classEntity in classes)
        {
            var modules = await _unitOfWork.Modules.GetAllAsync(
                m => m.ProgramId == classEntity.ProgramId && !m.IsDeleted);

            if (modules.Count == 0)
            {
                continue;
            }

            var orderedModules = modules.OrderBy(m => m.ModuleOrder).ToList();
            var moduleIds = orderedModules.Select(m => m.Id).ToList();

            var courses = await _unitOfWork.Courses.GetAllAsync(
                c => moduleIds.Contains(c.ModuleId) && !c.IsDeleted);
            var courseIds = courses.Select(c => c.Id).ToHashSet();
            var courseById = courses.ToDictionary(c => c.Id);

            var activities = await _unitOfWork.Activities.GetAllAsync(
                a => courseIds.Contains(a.CourseId)
                     && !a.IsDeleted
                     && a.ActivityType != ActivityType.SelfPaced);

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

            var missingItems = new List<(Guid ModuleId, Activity? Activity, Assignment? Assignment)>();
            foreach (var module in orderedModules)
            {
                var moduleActivities = activities
                    .Where(a => courseById[a.CourseId].ModuleId == module.Id)
                    .OrderBy(a => courseById[a.CourseId].CourseOrder)
                    .ThenBy(a => a.ActivityOrder)
                    .ToList();

                missingItems.AddRange(moduleActivities
                    .Where(a => !coveredActivityIds.Contains(a.Id))
                    .Select(a => (module.Id, (Activity?)a, (Assignment?)null)));

                missingItems.AddRange(assignments
                    .Where(a => a.ModuleId == module.Id && !coveredAssignmentIds.Contains(a.Id))
                    .Select(a => (module.Id, (Activity?)null, (Assignment?)a)));
            }

            if (missingItems.Count == 0)
            {
                continue;
            }

            var totalSessions = existingSessions.Count + missingItems.Count;
            var rangeDays = Math.Max((classEntity.EndDate.Date - classEntity.StartDate.Date).TotalDays, 1);

            for (var i = 0; i < missingItems.Count; i++)
            {
                var (moduleId, activity, assignment) = missingItems[i];
                var sessionIndex = existingSessions.Count + i;

                // Same spread as the template seeders: sessions distributed across the
                // class window, alternating morning/afternoon slots.
                var fraction = (sessionIndex + 1) / (double)(totalSessions + 1);
                var sessionDate = classEntity.StartDate.Date.AddDays(rangeDays * fraction);
                var startHour = sessionIndex % 2 == 0 ? 9 : 14;
                var durationMinutes = activity?.DurationMinutes ?? 60;
                var startTime = sessionDate.AddHours(startHour);
                var endTime = startTime.AddMinutes(durationMinutes);

                if (endTime > classEntity.EndDate)
                {
                    classEntity.EndDate = endTime.Date.AddDays(1);
                    if (!classesToExtend.Contains(classEntity))
                    {
                        classesToExtend.Add(classEntity);
                    }
                }

                sessionsToAdd.Add(new ClassSession
                {
                    Id = Guid.NewGuid(),
                    ClassId = classEntity.Id,
                    ModuleId = moduleId,
                    ActivityId = activity?.Id,
                    AssignmentId = assignment?.Id,
                    SessionKind = assignment != null ? SessionKind.AssignmentWindow : SessionKind.Lesson,
                    Title = activity?.Name ?? assignment!.Title,
                    Description = assignment != null
                        ? "Assignment working window and deadline checkpoint."
                        : activity!.Description,
                    StartTime = startTime,
                    EndTime = endTime,
                    Location = null,
                    RequiresAttendance = activity != null,
                    Status = endTime <= seedTime
                        ? ClassSessionStatus.Completed
                        : ClassSessionStatus.Scheduled,
                    CreatedAt = seedTime,
                    CreatedBy = Guid.Empty,
                    IsDeleted = false,
                });
            }
        }

        foreach (var classEntity in classesToExtend)
        {
            await _unitOfWork.Classes.Update(classEntity);
        }

        if (sessionsToAdd.Count == 0)
        {
            _loggerService.LogInformation("All classes already have full session coverage.");
            return;
        }

        await _unitOfWork.ClassSessions.AddRangeAsync(sessionsToAdd);
        await _unitOfWork.SaveChangesAsync();

        _loggerService.LogInformation(
            "Class session coverage top-up — {Count} session(s) added across {ClassCount} class(es).",
            sessionsToAdd.Count,
            sessionsToAdd.Select(s => s.ClassId).Distinct().Count());
    }
}
