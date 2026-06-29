using Microsoft.Extensions.Logging;
using OboxSteam.Domain.Entities;
using OboxSteam.Domain.Enums;

namespace OboxSteam.Application.Services;

public partial class SeedService
{
    private static readonly string[] OpenClassModuleCodes =
    [
        "MOD-ROBOTICS-01",
        "MOD-ROBOTICS-02",
        "MOD-ROBOTICS-03",
    ];

    private static readonly (string ModuleCode, string ActivityCode, SessionKind SessionKind, string Title, string Description)[]
        OpenClassSessionTemplatesPerModule =
        [
            ("MOD-ROBOTICS-01", "ACT-ROBOTICS-01-02", SessionKind.LiveOnline,
                "Module 1 Session 1: Introduction to Robotics",
                "Live cohort session covering robotics fundamentals."),
            ("MOD-ROBOTICS-01", "ACT-ROBOTICS-02-02", SessionKind.LiveOnline,
                "Module 1 Session 2: Actuator Design",
                "Live cohort session on actuators and mechanical design."),
            ("MOD-ROBOTICS-02", "ACT-ROBOTICS-04-02", SessionKind.LiveOnline,
                "Module 2 Session 1: Field Trip Preparation",
                "Live mentor briefing before the sensor exploration field trip."),
            ("MOD-ROBOTICS-02", "ACT-ROBOTICS-05-02", SessionKind.LiveOnline,
                "Module 2 Session 2: Movement Trip Preparation",
                "Live mentor session before the motor control field challenge."),
            ("MOD-ROBOTICS-03", "ACT-ROBOTICS-07-02", SessionKind.LiveOnline,
                "Module 3 Session 1: Prototype Build Preparation",
                "Live mentor session on team roles and build-day logistics."),
            ("MOD-ROBOTICS-03", "ACT-ROBOTICS-09-02", SessionKind.LiveOnline,
                "Module 3 Session 2: Final Testing Preparation",
                "Live mentor session before the capstone showcase."),
        ];

    private async Task SeedClassSessionsAsync()
    {
        _loggerService.LogInformation("Starting seed class sessions for open classes");

        var existingSession = await _unitOfWork.ClassSessions.FirstOrDefaultAsync(
            cs => OpenClassCodes.Contains(cs.Class.Code) && !cs.IsDeleted,
            cs => cs.Class);

        if (existingSession != null)
        {
            _loggerService.LogInformation("Open class sessions already seeded, skipping");
            return;
        }

        var modulesByCode = new Dictionary<string, Module>(StringComparer.OrdinalIgnoreCase);
        foreach (var moduleCode in OpenClassModuleCodes)
        {
            var module = await _unitOfWork.Modules.FirstOrDefaultAsync(m => m.Code == moduleCode);
            if (module == null)
            {
                _loggerService.LogWarning("Module {ModuleCode} not found. Skipping open class session seeding.", moduleCode);
                return;
            }

            modulesByCode[moduleCode] = module;
        }

        var activitiesByCode = (await _unitOfWork.Activities.GetAllAsync(a => !a.IsDeleted))
            .ToDictionary(a => a.Code, a => a, StringComparer.OrdinalIgnoreCase);

        var seedTime = DateTime.UtcNow;
        var sessionsToAdd = new List<ClassSession>();

        foreach (var classCode in OpenClassCodes)
        {
            var classEntity = await _unitOfWork.Classes.FirstOrDefaultAsync(c => c.Code == classCode);
            if (classEntity == null)
            {
                _loggerService.LogWarning("Class {ClassCode} not found. Skipping sessions.", classCode);
                continue;
            }

            var sessionIndex = 0;
            foreach (var template in OpenClassSessionTemplatesPerModule)
            {
                if (!modulesByCode.TryGetValue(template.ModuleCode, out var module))
                {
                    continue;
                }

                if (!activitiesByCode.TryGetValue(template.ActivityCode, out var activity))
                {
                    _loggerService.LogWarning(
                        "Activity {ActivityCode} not found for open class session. Skipping.",
                        template.ActivityCode);
                    sessionIndex++;
                    continue;
                }

                var (startTime, endTime) = ResolveOpenClassSessionTimes(classEntity, sessionIndex);

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

                sessionIndex++;
            }
        }

        if (sessionsToAdd.Count == 0)
        {
            _loggerService.LogWarning("No open class sessions created.");
            return;
        }

        await _unitOfWork.ClassSessions.AddRangeAsync(sessionsToAdd);
        await _unitOfWork.SaveChangesAsync();

        _loggerService.LogInformation(
            "Finished seed class sessions — {Count} session(s) created.",
            sessionsToAdd.Count);
    }

    private static (DateTime StartTime, DateTime EndTime) ResolveOpenClassSessionTimes(
        Class classEntity,
        int sessionIndex)
    {
        var durationDays = Math.Max((classEntity.EndDate.Date - classEntity.StartDate.Date).TotalDays, 1);
        var fractions = new[] { 0.10, 0.22, 0.34, 0.46, 0.58, 0.70 };
        var fraction = fractions[Math.Min(sessionIndex, fractions.Length - 1)];
        var sessionDate = classEntity.StartDate.Date.AddDays(durationDays * fraction);
        var startHour = sessionIndex % 2 == 0 ? 9 : 14;
        var startTime = sessionDate.AddHours(startHour);
        var endTime = startTime.AddHours(2).AddMinutes(30);
        return (startTime, endTime);
    }
}
