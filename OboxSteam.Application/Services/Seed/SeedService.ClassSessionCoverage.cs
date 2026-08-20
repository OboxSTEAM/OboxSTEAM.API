using Microsoft.Extensions.Logging;
using OboxSteam.Domain.Entities;
using OboxSteam.Domain.Enums;

namespace OboxSteam.Application.Services;

public partial class SeedService
{
    /// <summary>
    /// Top-up pass for ReadyForMentor/Open/InProgress classes: one session per missing
    /// LiveOnline/Offline activity and assignment, placed inside the existing class window
    /// (never extends EndDate). Draft, Completed, and Cancelled classes are left untouched.
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
                ];

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
            foreach (var module in modules)
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

            var nextIndex = existingSessions.Count;
            foreach (var (moduleId, activity, assignment) in missingItems)
            {
                var slot = SeedTimeline.TryResolveSlotSequence(
                    classEntity.StartDate,
                    classEntity.EndDate,
                    weeklySlots,
                    nextIndex);
                nextIndex++;
                if (slot == null)
                {
                    continue;
                }

                var durationMinutes = activity?.DurationMinutes ?? 60;
                var startTime = slot.Value.StartTime;
                var endTime = startTime.AddMinutes(durationMinutes);
                if (endTime.Date > classEntity.EndDate.Date)
                {
                    continue;
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
                    Status = SeedTimeline.ResolveSessionStatus(startTime, endTime, _seedNow),
                    CreatedAt = classEntity.CreatedAt,
                    CreatedBy = Guid.Empty,
                    IsDeleted = false,
                });
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
}
