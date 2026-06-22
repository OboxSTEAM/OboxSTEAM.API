using Microsoft.Extensions.Logging;
using OboxSteam.Application.Commons;
using OboxSteam.Application.Interfaces;
using OboxSteam.Application.Utils;
using OboxSteam.Domain.Entities;
using OboxSteam.Domain.Enums;
using OboxSteam.Domain.Interfaces;

namespace OboxSteam.Application.Services;

public partial class SeedService
{
    private async Task SeedRoboticsClassSessionsAsync()
    {
        _loggerService.LogInformation("Starting seed robotics class sessions");

        var existingSession = await _unitOfWork.ClassSessions.FirstOrDefaultAsync(
            cs => RoboticsClassCodes.Contains(cs.Class.Code) && !cs.IsDeleted,
            cs => cs.Class);

        if (existingSession != null)
        {
            _loggerService.LogInformation("Robotics class sessions already exist, skipping seeding");
            return;
        }

        var moduleTheory = await _unitOfWork.Modules.FirstOrDefaultAsync(m => m.Code == "MOD-ROBOTICS-01");
        var moduleExperiential = await _unitOfWork.Modules.FirstOrDefaultAsync(m => m.Code == "MOD-ROBOTICS-02");
        var moduleResearch = await _unitOfWork.Modules.FirstOrDefaultAsync(m => m.Code == "MOD-ROBOTICS-03");

        if (moduleTheory == null || moduleExperiential == null || moduleResearch == null)
        {
            _loggerService.LogWarning("Robotics modules not found. Skipping robotics class session seeding.");
            return;
        }

        var modulesByCode = new Dictionary<string, Module>(StringComparer.OrdinalIgnoreCase)
        {
            [moduleTheory.Code] = moduleTheory,
            [moduleExperiential.Code] = moduleExperiential,
            [moduleResearch.Code] = moduleResearch,
        };

        var sessionTemplates = GetRoboticsClassSessionTemplates();
        var activitiesByCode = (await _unitOfWork.Activities.GetAllAsync(a => !a.IsDeleted))
            .ToDictionary(a => a.Code, a => a, StringComparer.OrdinalIgnoreCase);

        var seedTime = DateTime.UtcNow;
        var sessionsToAdd = new List<ClassSession>();
        var anchorDate = seedTime.Date;

        foreach (var classCode in RoboticsClassCodes)
        {
            var classEntity = await _unitOfWork.Classes.FirstOrDefaultAsync(c => c.Code == classCode);
            if (classEntity == null)
            {
                _loggerService.LogWarning("Robotics class {ClassCode} not found. Skipping sessions.", classCode);
                continue;
            }

            var isMentor1Class = Mentor1RoboticsClassCodes.Contains(classCode);
            var isMentor1ClassA = string.Equals(classCode, "CLS-ROBOTICS-2026A", StringComparison.OrdinalIgnoreCase);

            for (var sessionIndex = 0; sessionIndex < sessionTemplates.Count; sessionIndex++)
            {
                var template = sessionTemplates[sessionIndex];
                if (!modulesByCode.TryGetValue(template.ModuleCode, out var module))
                {
                    continue;
                }

                if (!activitiesByCode.TryGetValue(template.ActivityCode, out var activity))
                {
                    _loggerService.LogWarning(
                        "Activity {ActivityCode} not found for robotics class session. Skipping.",
                        template.ActivityCode);
                    continue;
                }

                var (startTime, endTime) = ResolveRoboticsSessionTimes(
                    classEntity,
                    sessionIndex,
                    isMentor1Class,
                    isMentor1ClassA,
                    anchorDate);

                sessionsToAdd.Add(new ClassSession
                {
                    Id = Guid.NewGuid(),
                    ClassId = classEntity.Id,
                    ModuleId = module.Id,
                    ActivityId = activity.Id,
                    SessionKind = template.SessionKind,
                    Title = template.Title,
                    Description = template.Description,
                    StartTime = startTime,
                    EndTime = endTime,
                    Location = activity.Location,
                    RequiresAttendance = activity.ActivityType != ActivityType.SelfPaced,
                    Status = ClassSessionStatus.Scheduled,
                    CreatedAt = seedTime,
                    CreatedBy = Guid.Empty,
                    IsDeleted = false,
                });
            }
        }

        if (sessionsToAdd.Count == 0)
        {
            _loggerService.LogWarning("No robotics class sessions created.");
            return;
        }

        await _unitOfWork.ClassSessions.AddRangeAsync(sessionsToAdd);
        await _unitOfWork.SaveChangesAsync();

        _loggerService.LogInformation(
            "Finished seed robotics class sessions — {Count} session(s) created.",
            sessionsToAdd.Count);
    }

    private static IReadOnlyList<RoboticsClassSessionTemplate> GetRoboticsClassSessionTemplates() =>
    [
        new("MOD-ROBOTICS-01", "ACT-ROBOTICS-01-02", SessionKind.LiveOnline,
            "Theory Session 1: Introduction to Robotics",
            "Live cohort session covering robotics fundamentals."),
        new("MOD-ROBOTICS-01", "ACT-ROBOTICS-02-02", SessionKind.LiveOnline,
            "Theory Session 2: Actuator Design",
            "Live cohort session on actuators and mechanical design."),
        new("MOD-ROBOTICS-02", "ACT-ROBOTICS-04-02", SessionKind.Lesson,
            "Experiential Session 1: Sensor Exploration Lab",
            "Hands-on sensor exploration in the electronics lab."),
        new("MOD-ROBOTICS-02", "ACT-ROBOTICS-05-02", SessionKind.LiveOnline,
            "Experiential Session 2: Movement Patterns Workshop",
            "Live workshop on programming robot movement."),
        new("MOD-ROBOTICS-03", "ACT-ROBOTICS-07-02", SessionKind.Lesson,
            "Research Session 1: Team Prototype Build",
            "Full-day team build session for the capstone prototype."),
        new("MOD-ROBOTICS-03", "ACT-ROBOTICS-07-03", SessionKind.LiveOnline,
            "Research Session 2: Capstone Presentation",
            "Live capstone presentation and mentor Q&A."),
    ];

    private static (DateTime StartTime, DateTime EndTime) ResolveRoboticsSessionTimes(
        Class classEntity,
        int sessionIndex,
        bool isMentor1Class,
        bool isMentor1ClassA,
        DateTime anchorDate)
    {
        if (isMentor1Class && Mentor1SharedSessionDayOffsets.TryGetValue(sessionIndex, out var sharedDayOffset))
        {
            var sharedDate = anchorDate.AddDays(sharedDayOffset);
            var morningStart = sharedDate.AddHours(9);
            var morningEnd = sharedDate.AddHours(11).AddMinutes(30);
            var afternoonStart = sharedDate.AddHours(14);
            var afternoonEnd = sharedDate.AddHours(16).AddMinutes(30);

            return isMentor1ClassA
                ? (morningStart, morningEnd)
                : (afternoonStart, afternoonEnd);
        }

        var durationDays = Math.Max((classEntity.EndDate.Date - classEntity.StartDate.Date).TotalDays, 1);
        var fractions = new[] { 0.08, 0.24, 0.40, 0.56, 0.72, 0.88 };
        var fraction = fractions[Math.Min(sessionIndex, fractions.Length - 1)];
        var sessionDate = classEntity.StartDate.Date.AddDays(durationDays * fraction);
        var startHour = sessionIndex % 2 == 0 ? 9 : 14;
        var startTime = sessionDate.AddHours(startHour);
        var endTime = startTime.AddHours(2).AddMinutes(30);
        return (startTime, endTime);
    }

    private sealed record RoboticsClassSessionTemplate(
        string ModuleCode,
        string ActivityCode,
        SessionKind SessionKind,
        string Title,
        string Description);
}

