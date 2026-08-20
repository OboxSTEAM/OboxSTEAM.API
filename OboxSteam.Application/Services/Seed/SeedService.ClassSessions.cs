using Microsoft.Extensions.Logging;
using OboxSteam.Domain.Entities;
using OboxSteam.Domain.Enums;

namespace OboxSteam.Application.Services;

public partial class SeedService
{
    private async Task SeedAcademicYearSessionsAsync()
    {
        _loggerService.LogInformation("Starting seed academic-year class sessions");

        var existingSession = await _unitOfWork.ClassSessions.FirstOrDefaultAsync(
            cs => !cs.IsDeleted);
        if (existingSession != null)
        {
            _loggerService.LogInformation("Class sessions already exist, skipping");
            return;
        }

        var classByCode = (await _unitOfWork.Classes.GetAllAsync(c => !c.IsDeleted))
            .ToDictionary(c => c.Code, c => c, StringComparer.OrdinalIgnoreCase);
        var allModules = await _unitOfWork.Modules.GetAllAsync(m => !m.IsDeleted);
        var modulesByCode = allModules.ToDictionary(m => m.Code, m => m, StringComparer.OrdinalIgnoreCase);
        var activities = await _unitOfWork.Activities.GetAllAsync(a => !a.IsDeleted);
        var activitiesByCode = activities.ToDictionary(a => a.Code, a => a, StringComparer.OrdinalIgnoreCase);
        var courses = await _unitOfWork.Courses.GetAllAsync(c => !c.IsDeleted);
        var assignments = await _unitOfWork.Assignments.GetAllAsync(a => !a.IsDeleted);

        var sessionsToAdd = new List<ClassSession>();

        foreach (var definition in GetAcademicYearClassDefinitions())
        {
            if (!classByCode.TryGetValue(definition.Code, out var classEntity))
            {
                continue;
            }

            if (definition.ProgramCode == "PRG-ROBOTICS")
            {
                sessionsToAdd.AddRange(BuildRoboticsSessions(
                    classEntity,
                    definition.WeeklySlots,
                    modulesByCode,
                    activitiesByCode));
                continue;
            }

            sessionsToAdd.AddRange(BuildCurriculumSessions(
                classEntity,
                definition.WeeklySlots,
                allModules,
                courses,
                activities,
                assignments));
        }

        if (sessionsToAdd.Count == 0)
        {
            _loggerService.LogWarning("No academic-year class sessions created.");
            return;
        }

        await _unitOfWork.ClassSessions.AddRangeAsync(sessionsToAdd);
        await _unitOfWork.SaveChangesAsync();
        _loggerService.LogInformation(
            "Finished seed academic-year class sessions — {Count} session(s) created.",
            sessionsToAdd.Count);

        await SeedSessionAttendanceAsync();
    }

    private List<ClassSession> BuildRoboticsSessions(
        Class classEntity,
        SeedTimeline.WeekdaySlot[] weeklySlots,
        IReadOnlyDictionary<string, Module> modulesByCode,
        IReadOnlyDictionary<string, Activity> activitiesByCode)
    {
        var sessions = new List<ClassSession>();
        var sessionIndex = 0;

        foreach (var template in IntroductionToRoboticsClassSessionTemplates)
        {
            if (!modulesByCode.TryGetValue(template.ModuleCode, out var module)
                || !activitiesByCode.TryGetValue(template.ActivityCode, out var activity))
            {
                sessionIndex++;
                continue;
            }

            var slot = SeedTimeline.TryResolveSlotSequence(
                classEntity.StartDate,
                classEntity.EndDate,
                weeklySlots,
                sessionIndex);
            sessionIndex++;
            if (slot == null)
            {
                continue;
            }

            sessions.Add(CreateSeedSession(
                classEntity,
                module.Id,
                activity.Id,
                null,
                template.SessionKind,
                template.Title,
                template.Description,
                slot.Value.StartTime,
                slot.Value.EndTime,
                activity.ActivityType != ActivityType.SelfPaced));
        }

        return sessions;
    }

    private List<ClassSession> BuildCurriculumSessions(
        Class classEntity,
        SeedTimeline.WeekdaySlot[] weeklySlots,
        IReadOnlyList<Module> allModules,
        IReadOnlyList<Course> courses,
        IReadOnlyList<Activity> activities,
        IReadOnlyList<Assignment> assignments)
    {
        var sessions = new List<ClassSession>();
        var modules = allModules
            .Where(m => m.ProgramId == classEntity.ProgramId)
            .OrderBy(m => m.ModuleOrder)
            .ToList();

        var sessionIndex = 0;
        foreach (var module in modules)
        {
            var moduleCourseIds = courses
                .Where(c => c.ModuleId == module.Id)
                .Select(c => c.Id)
                .ToHashSet();
            var liveActivities = activities
                .Where(a => moduleCourseIds.Contains(a.CourseId)
                            && a.ActivityType != ActivityType.SelfPaced)
                .OrderBy(a => a.ActivityOrder)
                .ToList();

            foreach (var activity in liveActivities)
            {
                var slot = SeedTimeline.TryResolveSlotSequence(
                    classEntity.StartDate,
                    classEntity.EndDate,
                    weeklySlots,
                    sessionIndex);
                sessionIndex++;
                if (slot == null)
                {
                    continue;
                }

                sessions.Add(CreateSeedSession(
                    classEntity,
                    module.Id,
                    activity.Id,
                    null,
                    SessionKind.Lesson,
                    activity.Name,
                    activity.Description,
                    slot.Value.StartTime,
                    slot.Value.EndTime,
                    true));
            }

            foreach (var assignment in assignments.Where(a => a.ModuleId == module.Id))
            {
                var slot = SeedTimeline.TryResolveSlotSequence(
                    classEntity.StartDate,
                    classEntity.EndDate,
                    weeklySlots,
                    sessionIndex);
                sessionIndex++;
                if (slot == null)
                {
                    continue;
                }

                sessions.Add(CreateSeedSession(
                    classEntity,
                    module.Id,
                    null,
                    assignment.Id,
                    SessionKind.AssignmentWindow,
                    assignment.Title,
                    "Assignment working window and deadline checkpoint.",
                    slot.Value.StartTime,
                    slot.Value.EndTime,
                    false));
            }
        }

        return sessions;
    }

    private ClassSession CreateSeedSession(
        Class classEntity,
        Guid moduleId,
        Guid? activityId,
        Guid? assignmentId,
        SessionKind kind,
        string title,
        string? description,
        DateTime startTime,
        DateTime endTime,
        bool requiresAttendance)
    {
        return new ClassSession
        {
            Id = Guid.NewGuid(),
            ClassId = classEntity.Id,
            ModuleId = moduleId,
            ActivityId = activityId,
            AssignmentId = assignmentId,
            SessionKind = kind,
            Title = title,
            Description = description,
            StartTime = startTime,
            EndTime = endTime,
            Location = null,
            RequiresAttendance = requiresAttendance,
            Status = SeedTimeline.ResolveSessionStatus(startTime, endTime, _seedNow),
            CreatedAt = classEntity.CreatedAt,
            CreatedBy = Guid.Empty,
            IsDeleted = false,
        };
    }

    private async Task SeedSessionAttendanceAsync()
    {
        var completedSessions = await _unitOfWork.ClassSessions.GetAllAsync(
            cs => !cs.IsDeleted
                  && cs.RequiresAttendance
                  && cs.Status == ClassSessionStatus.Completed);
        if (completedSessions.Count == 0)
        {
            return;
        }

        var classEnrollments = await _unitOfWork.ClassEnrollments.GetAllAsync(
            ce => !ce.IsDeleted
                  && (ce.Status == ClassEnrollmentStatus.Active
                      || ce.Status == ClassEnrollmentStatus.Completed));
        var enrollmentsByClass = classEnrollments
            .GroupBy(ce => ce.ClassId)
            .ToDictionary(g => g.Key, g => g.ToList());
        var moduleEnrollments = await _unitOfWork.ModuleEnrollments.GetAllAsync(me => !me.IsDeleted);
        var attendances = new List<SessionAttendance>();

        foreach (var session in completedSessions)
        {
            if (!enrollmentsByClass.TryGetValue(session.ClassId, out var roster))
            {
                continue;
            }

            for (var studentIndex = 0; studentIndex < roster.Count; studentIndex++)
            {
                var enrollment = roster[studentIndex];
                var moduleEnrollment = moduleEnrollments.FirstOrDefault(
                    me => me.StudentId == enrollment.StudentId
                          && me.ModuleId == session.ModuleId
                          && !me.IsDeleted);
                if (moduleEnrollment == null)
                {
                    continue;
                }

                var status = SeedTimeline.AttendanceForIndex(studentIndex, session.StartTime.DayOfYear);
                DateTime? checkedInAt = status is AttendanceStatus.Present or AttendanceStatus.Late
                    ? session.StartTime.AddMinutes(status == AttendanceStatus.Late ? 12 : 2)
                    : null;

                attendances.Add(new SessionAttendance
                {
                    Id = Guid.NewGuid(),
                    ClassSessionId = session.Id,
                    StudentId = enrollment.StudentId,
                    ModuleEnrollmentId = moduleEnrollment.Id,
                    Status = status,
                    CheckedInAt = checkedInAt,
                    CreatedAt = session.StartTime,
                    CreatedBy = Guid.Empty,
                    IsDeleted = false,
                });
            }
        }

        if (attendances.Count == 0)
        {
            return;
        }

        await _unitOfWork.SessionAttendances.AddRangeAsync(attendances);
        await _unitOfWork.SaveChangesAsync();
        _loggerService.LogInformation(
            "Finished seed session attendance — {Count} row(s).",
            attendances.Count);
    }
}
