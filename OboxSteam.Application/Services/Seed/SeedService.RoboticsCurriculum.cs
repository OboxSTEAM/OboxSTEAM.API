using Microsoft.Extensions.Logging;
using OboxSteam.Domain.Entities;
using OboxSteam.Domain.Enums;

namespace OboxSteam.Application.Services;

public partial class SeedService
{
    private static readonly string[] RoboticsCourseCodes =
    [
        "CRS-ROBOTICS-01",
        "CRS-ROBOTICS-02",
        "CRS-ROBOTICS-03",
        "CRS-ROBOTICS-04",
        "CRS-ROBOTICS-05",
        "CRS-ROBOTICS-06",
        "CRS-ROBOTICS-07",
        "CRS-ROBOTICS-08",
        "CRS-ROBOTICS-09",
    ];

    private static readonly Dictionary<string, string[]> RoboticsMilestoneActivityCodes =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["RML-ROBOTICS-03-01"] =
            [
                "ACT-ROBOTICS-07-01",
                "ACT-ROBOTICS-07-02",
                "ACT-ROBOTICS-07-03",
            ],
            ["RML-ROBOTICS-03-02"] =
            [
                "ACT-ROBOTICS-08-01",
                "ACT-ROBOTICS-08-02",
                "ACT-ROBOTICS-08-03",
            ],
            ["RML-ROBOTICS-03-03"] =
            [
                "ACT-ROBOTICS-09-01",
                "ACT-ROBOTICS-09-02",
                "ACT-ROBOTICS-09-03",
            ],
        };

    /// <summary>
    /// Upserts robotics curriculum when seed data already exists (idempotent re-seed).
    /// </summary>
    private async Task SyncRoboticsCurriculumAsync()
    {
        var program = await _unitOfWork.Programs.FirstOrDefaultAsync(
            p => p.Code == "PRG-ROBOTICS" && !p.IsDeleted);
        if (program == null)
        {
            _loggerService.LogWarning("PRG-ROBOTICS not found. Skipping robotics curriculum sync.");
            return;
        }

        var moduleTheory = await _unitOfWork.Modules.FirstOrDefaultAsync(
            m => m.Code == "MOD-ROBOTICS-01" && !m.IsDeleted);
        var moduleExperiential = await _unitOfWork.Modules.FirstOrDefaultAsync(
            m => m.Code == "MOD-ROBOTICS-02" && !m.IsDeleted);
        var moduleResearch = await _unitOfWork.Modules.FirstOrDefaultAsync(
            m => m.Code == "MOD-ROBOTICS-03" && !m.IsDeleted);

        if (moduleTheory == null && moduleExperiential == null && moduleResearch == null)
        {
            _loggerService.LogWarning("Robotics modules not found. Skipping robotics curriculum sync.");
            return;
        }

        _loggerService.LogInformation("Starting robotics curriculum sync");

        await EnsureRoboticsCoursesAsync(moduleTheory, moduleExperiential, moduleResearch);
        await SyncRoboticsActivitiesAsync();
        await SyncRoboticsResearchMilestoneActivitiesAsync();
        await SyncRoboticsMaterialsAsync();
        await ResyncRoboticsClassSessionsIfNeededAsync();

        _loggerService.LogInformation("Finished robotics curriculum sync");
    }

    private async Task EnsureRoboticsCoursesAsync(
        Module? moduleTheory,
        Module? moduleExperiential,
        Module? moduleResearch)
    {
        var existingCourses = await _unitOfWork.Courses.GetAllAsync(
            c => RoboticsCourseCodes.Contains(c.Code) && !c.IsDeleted);
        var existingCodes = existingCourses
            .Select(c => c.Code)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var seedTime = DateTime.UtcNow;
        var templateCourses = new List<Course>();
        AddRoboticsCourses(templateCourses, moduleTheory, moduleExperiential, moduleResearch, seedTime);

        var coursesToAdd = templateCourses
            .Where(c => !existingCodes.Contains(c.Code))
            .ToList();

        if (coursesToAdd.Count == 0)
        {
            return;
        }

        await _unitOfWork.Courses.AddRangeAsync(coursesToAdd);
        await _unitOfWork.SaveChangesAsync();

        _loggerService.LogInformation(
            "Robotics curriculum sync — added {Count} missing course(s).",
            coursesToAdd.Count);
    }

    private async Task SyncRoboticsActivitiesAsync()
    {
        var roboticsCourses = await _unitOfWork.Courses.GetAllAsync(
            c => RoboticsCourseCodes.Contains(c.Code) && !c.IsDeleted);
        if (roboticsCourses.Count == 0)
        {
            _loggerService.LogWarning("No robotics courses found. Skipping robotics activity sync.");
            return;
        }

        var courseByCode = roboticsCourses.ToDictionary(c => c.Code, c => c);
        var seedTime = DateTime.UtcNow;
        var baseDate = seedTime.Date;
        var templateActivities = BuildRoboticsSeedActivities(courseByCode, baseDate, seedTime);
        if (templateActivities.Count == 0)
        {
            return;
        }

        var templateByCode = templateActivities.ToDictionary(a => a.Code, StringComparer.OrdinalIgnoreCase);
        var existingActivities = await _unitOfWork.Activities.GetAllAsync(
            a => templateByCode.Keys.Contains(a.Code) && !a.IsDeleted);
        var existingByCode = existingActivities.ToDictionary(a => a.Code, StringComparer.OrdinalIgnoreCase);

        var activitiesToAdd = new List<Activity>();
        var updatedCount = 0;

        foreach (var (code, template) in templateByCode)
        {
            if (existingByCode.TryGetValue(code, out var existing))
            {
                if (ApplyRoboticsActivityTemplate(existing, template))
                {
                    await _unitOfWork.Activities.Update(existing);
                    updatedCount++;
                }

                continue;
            }

            activitiesToAdd.Add(template);
        }

        if (activitiesToAdd.Count > 0)
        {
            await _unitOfWork.Activities.AddRangeAsync(activitiesToAdd);
        }

        if (activitiesToAdd.Count > 0 || updatedCount > 0)
        {
            await _unitOfWork.SaveChangesAsync();
            _loggerService.LogInformation(
                "Robotics curriculum sync — {Created} activity(ies) created, {Updated} updated.",
                activitiesToAdd.Count,
                updatedCount);
        }
    }

    private static List<Activity> BuildRoboticsSeedActivities(
        Dictionary<string, Course> courseByCode,
        DateTime baseDate,
        DateTime seedTime)
    {
        var activities = new List<Activity>();
        AddRoboticsSeedActivities(activities, courseByCode, baseDate, seedTime);
        return activities;
    }

    private static bool ApplyRoboticsActivityTemplate(Activity existing, Activity template)
    {
        var changed = false;

        void Set<T>(Func<T> current, T value, Action<T> assign)
        {
            if (!EqualityComparer<T>.Default.Equals(current(), value))
            {
                assign(value);
                changed = true;
            }
        }

        Set(() => existing.Name, template.Name, v => existing.Name = v);
        Set(() => existing.ActivityType, template.ActivityType, v => existing.ActivityType = v);
        Set(() => existing.Description, template.Description, v => existing.Description = v);
        Set(() => existing.ActivityOrder, template.ActivityOrder, v => existing.ActivityOrder = v);
        Set(() => existing.Location, template.Location, v => existing.Location = v);
        Set(() => existing.StartTime, template.StartTime, v => existing.StartTime = v);
        Set(() => existing.EndTime, template.EndTime, v => existing.EndTime = v);
        Set(() => existing.MaxCapacity, template.MaxCapacity, v => existing.MaxCapacity = v);
        Set(() => existing.RequireQrCheckin, template.RequireQrCheckin, v => existing.RequireQrCheckin = v);
        Set(
            () => existing.RequireMediaEvidence,
            template.RequireMediaEvidence,
            v => existing.RequireMediaEvidence = v);
        Set(() => existing.CourseId, template.CourseId, v => existing.CourseId = v);

        return changed;
    }

    private async Task SyncRoboticsResearchMilestoneActivitiesAsync()
    {
        var moduleResearch = await _unitOfWork.Modules.FirstOrDefaultAsync(
            m => m.Code == "MOD-ROBOTICS-03" && !m.IsDeleted);
        if (moduleResearch == null)
        {
            return;
        }

        var milestones = await _unitOfWork.ResearchMilestones.GetAllAsync(
            rm => rm.ModuleId == moduleResearch.Id && !rm.IsDeleted);
        if (milestones.Count == 0)
        {
            return;
        }

        var milestoneByCode = milestones.ToDictionary(m => m.Code, StringComparer.OrdinalIgnoreCase);
        var milestoneIds = milestones.Select(m => m.Id).ToList();
        var existingLinks = await _unitOfWork.ResearchMilestoneActivities.GetAllAsync(
            rma => milestoneIds.Contains(rma.ResearchMilestoneId) && !rma.IsDeleted);

        var activitiesByCode = (await _unitOfWork.Activities.GetAllAsync(
                a => a.Code.StartsWith("ACT-ROBOTICS-") && !a.IsDeleted))
            .ToDictionary(a => a.Code, a => a, StringComparer.OrdinalIgnoreCase);

        if (!RoboticsMilestoneLinksNeedSync(milestoneByCode, existingLinks, activitiesByCode))
        {
            _loggerService.LogInformation("Robotics research milestone links are up to date.");
            return;
        }

        if (existingLinks.Count > 0)
        {
            await _unitOfWork.ResearchMilestoneActivities.HardRemove(
                rma => milestoneIds.Contains(rma.ResearchMilestoneId));
            await _unitOfWork.SaveChangesAsync();
        }

        var seedTime = DateTime.UtcNow;
        var linksToAdd = BuildRoboticsResearchMilestoneActivities(
            milestoneByCode,
            activitiesByCode,
            seedTime);

        if (linksToAdd.Count == 0)
        {
            _loggerService.LogWarning(
                "Could not build robotics milestone activity links — activities may be missing.");
            return;
        }

        await _unitOfWork.ResearchMilestoneActivities.AddRangeAsync(linksToAdd);
        await _unitOfWork.SaveChangesAsync();

        _loggerService.LogInformation(
            "Robotics curriculum sync — rebuilt {Count} research milestone activity link(s).",
            linksToAdd.Count);
    }

    private static bool RoboticsMilestoneLinksNeedSync(
        IReadOnlyDictionary<string, ResearchMilestone> milestoneByCode,
        IReadOnlyList<ResearchMilestoneActivity> existingLinks,
        IReadOnlyDictionary<string, Activity> activitiesByCode)
    {
        const int expectedLinkCount = 9;
        if (existingLinks.Count != expectedLinkCount)
        {
            return true;
        }

        foreach (var (milestoneCode, activityCodes) in RoboticsMilestoneActivityCodes)
        {
            if (!milestoneByCode.TryGetValue(milestoneCode, out var milestone))
            {
                return true;
            }

            var linksForMilestone = existingLinks
                .Where(l => l.ResearchMilestoneId == milestone.Id)
                .OrderBy(l => l.DisplayOrder)
                .ToList();

            if (linksForMilestone.Count != activityCodes.Length)
            {
                return true;
            }

            for (var i = 0; i < activityCodes.Length; i++)
            {
                if (!activitiesByCode.TryGetValue(activityCodes[i], out var expectedActivity))
                {
                    return true;
                }

                if (linksForMilestone[i].ActivityId != expectedActivity.Id)
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static List<ResearchMilestoneActivity> BuildRoboticsResearchMilestoneActivities(
        IReadOnlyDictionary<string, ResearchMilestone> milestoneByCode,
        IReadOnlyDictionary<string, Activity> activitiesByCode,
        DateTime seedTime)
    {
        var links = new List<ResearchMilestoneActivity>();

        foreach (var (milestoneCode, activityCodes) in RoboticsMilestoneActivityCodes)
        {
            if (!milestoneByCode.TryGetValue(milestoneCode, out var milestone))
            {
                continue;
            }

            for (var displayOrder = 0; displayOrder < activityCodes.Length; displayOrder++)
            {
                if (!activitiesByCode.TryGetValue(activityCodes[displayOrder], out var activity))
                {
                    continue;
                }

                links.Add(new ResearchMilestoneActivity
                {
                    Id = Guid.NewGuid(),
                    ResearchMilestoneId = milestone.Id,
                    ActivityId = activity.Id,
                    IsRequiredForSubmission = true,
                    DisplayOrder = displayOrder + 1,
                    CreatedAt = seedTime,
                    CreatedBy = Guid.Empty,
                    IsDeleted = false,
                });
            }
        }

        return links;
    }

    private async Task SyncRoboticsMaterialsAsync()
    {
        var definitions = GetRoboticsSeedMaterialDefinitions()
            .ToDictionary(d => d.ActivityCode, d => d, StringComparer.OrdinalIgnoreCase);

        var selfPacedActivities = await _unitOfWork.Activities.GetAllAsync(
            a => definitions.Keys.Contains(a.Code)
                 && !a.IsDeleted
                 && a.ActivityType == ActivityType.SelfPaced);

        if (selfPacedActivities.Count == 0)
        {
            return;
        }

        var activityIds = selfPacedActivities.Select(a => a.Id).ToList();
        var existingMaterials = await _unitOfWork.Materials.GetAllAsync(
            m => activityIds.Contains(m.ActivityId) && !m.IsDeleted);
        var materialByActivityId = existingMaterials.ToDictionary(m => m.ActivityId);

        var seedTime = DateTime.UtcNow;
        var materialsToAdd = new List<Material>();
        var updatedCount = 0;

        foreach (var activity in selfPacedActivities)
        {
            if (!definitions.TryGetValue(activity.Code, out var definition))
            {
                continue;
            }

            if (materialByActivityId.TryGetValue(activity.Id, out var existingMaterial))
            {
                var changed = false;

                if (!string.Equals(existingMaterial.Title, definition.Title, StringComparison.Ordinal))
                {
                    existingMaterial.Title = definition.Title;
                    changed = true;
                }

                if (existingMaterial.MaterialType != definition.MaterialType)
                {
                    existingMaterial.MaterialType = definition.MaterialType;
                    changed = true;
                }

                if (!string.Equals(existingMaterial.FileUrl, definition.FileUrl, StringComparison.Ordinal))
                {
                    existingMaterial.FileUrl = definition.FileUrl;
                    changed = true;
                }

                if (existingMaterial.FileSizeBytes != definition.FileSizeBytes)
                {
                    existingMaterial.FileSizeBytes = definition.FileSizeBytes;
                    changed = true;
                }

                if (changed)
                {
                    await _unitOfWork.Materials.Update(existingMaterial);
                    updatedCount++;
                }

                continue;
            }

            materialsToAdd.Add(new Material
            {
                Id = Guid.NewGuid(),
                ActivityId = activity.Id,
                Title = definition.Title,
                MaterialType = definition.MaterialType,
                FileUrl = definition.FileUrl,
                FileSizeBytes = definition.FileSizeBytes,
                CreatedAt = seedTime,
                CreatedBy = Guid.Empty,
                IsDeleted = false,
            });
        }

        if (materialsToAdd.Count > 0)
        {
            await _unitOfWork.Materials.AddRangeAsync(materialsToAdd);
        }

        if (materialsToAdd.Count > 0 || updatedCount > 0)
        {
            await _unitOfWork.SaveChangesAsync();
            _loggerService.LogInformation(
                "Robotics curriculum sync — {Created} material(s) created, {Updated} updated.",
                materialsToAdd.Count,
                updatedCount);
        }
    }

    private async Task ResyncRoboticsClassSessionsIfNeededAsync()
    {
        var sessionTemplates = GetRoboticsClassSessionTemplates();
        var roboticsClasses = await _unitOfWork.Classes.GetAllAsync(
            c => RoboticsClassCodes.Contains(c.Code) && !c.IsDeleted);

        if (roboticsClasses.Count == 0)
        {
            return;
        }

        var classIds = roboticsClasses.Select(c => c.Id).ToList();
        var existingSessions = await _unitOfWork.ClassSessions.GetAllAsync(
            cs => classIds.Contains(cs.ClassId) && !cs.IsDeleted);

        var sessionsPerClass = existingSessions
            .GroupBy(cs => cs.ClassId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var activitiesByCode = (await _unitOfWork.Activities.GetAllAsync(a => !a.IsDeleted))
            .ToDictionary(a => a.Code, a => a, StringComparer.OrdinalIgnoreCase);

        var needsResync = roboticsClasses.Any(classEntity =>
        {
            if (!sessionsPerClass.TryGetValue(classEntity.Id, out var classSessions))
            {
                return true;
            }

            if (classSessions.Count != sessionTemplates.Count)
            {
                return true;
            }

            for (var i = 0; i < sessionTemplates.Count; i++)
            {
                var template = sessionTemplates[i];
                if (!activitiesByCode.TryGetValue(template.ActivityCode, out var activity))
                {
                    return true;
                }

                if (!classSessions.Any(cs => cs.ActivityId == activity.Id))
                {
                    return true;
                }
            }

            return false;
        });

        if (!needsResync)
        {
            return;
        }

        await _unitOfWork.ClassSessions.HardRemove(cs => classIds.Contains(cs.ClassId));
        await _unitOfWork.SaveChangesAsync();

        _loggerService.LogInformation(
            "Robotics curriculum sync — cleared class sessions for {Count} class(es); re-seeding.",
            roboticsClasses.Count);

        await SeedRoboticsClassSessionsAsync();
    }
}
