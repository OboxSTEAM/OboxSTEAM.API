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
            foreach (var template in IntroductionToRoboticsClassSessionTemplates)
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

                var (startTime, endTime) = ResolveIntroductionToRoboticsSessionTimes(
                    classEntity,
                    sessionIndex,
                    IntroductionToRoboticsClassSessionTemplates.Length);

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
                    // Venue is set per session by the manager; curriculum activities no
                    // longer carry a template location.
                    Location = null,
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
}
