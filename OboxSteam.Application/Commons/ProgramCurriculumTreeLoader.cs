using OboxSteam.Application.Utils;
using OboxSteam.Domain.Entities;
using OboxSteam.Domain.Enums;
using OboxSteam.Domain.Interfaces;

namespace OboxSteam.Application.Commons;

/// <summary>
/// Loads program curriculum tree data from persistence.
/// </summary>
public static class ProgramCurriculumTreeLoader
{
    public static async Task<ProgramCurriculumTreeSnapshot> LoadAsync(IUnitOfWork unitOfWork, Guid programId)
    {
        var program = await unitOfWork.Programs.GetByIdAsync(programId, p => p.Modules);

        if (program == null || program.IsDeleted)
        {
            throw ErrorHelper.NotFound($"Program with id '{programId}' not found.");
        }

        var modules = program.Modules?
            .Where(m => !m.IsDeleted)
            .OrderBy(m => m.ModuleOrder)
            .ToList() ?? [];

        var theoryExperientialModuleIds = modules
            .Where(m => m.ModuleType != ModuleType.Research)
            .Select(m => m.Id)
            .ToList();

        var courses = theoryExperientialModuleIds.Count > 0
            ? await unitOfWork.Courses.GetAllAsync(
                c => theoryExperientialModuleIds.Contains(c.ModuleId) && !c.IsDeleted)
            : new List<Course>();

        var courseIds = courses.Select(c => c.Id).ToList();

        var courseActivities = courseIds.Count > 0
            ? await unitOfWork.Activities.GetAllAsync(
                a => courseIds.Contains(a.CourseId) && !a.IsDeleted)
            : new List<Activity>();

        var researchModuleIds = modules
            .Where(m => m.ModuleType == ModuleType.Research)
            .Select(m => m.Id)
            .ToList();

        var milestones = researchModuleIds.Count > 0
            ? await unitOfWork.ResearchMilestones.GetAllAsync(
                rm => researchModuleIds.Contains(rm.ModuleId) && !rm.IsDeleted)
            : new List<ResearchMilestone>();

        var milestoneIds = milestones.Select(m => m.Id).ToList();

        var milestoneActivityLinks = milestoneIds.Count > 0
            ? await unitOfWork.ResearchMilestoneActivities.GetAllAsync(
                rma => milestoneIds.Contains(rma.ResearchMilestoneId) && !rma.IsDeleted)
            : new List<ResearchMilestoneActivity>();

        var researchActivityIds = milestoneActivityLinks
            .Select(rma => rma.ActivityId)
            .Distinct()
            .ToList();

        var researchActivities = researchActivityIds.Count > 0
            ? await unitOfWork.Activities.GetAllAsync(
                a => researchActivityIds.Contains(a.Id) && !a.IsDeleted)
            : new List<Activity>();

        var allActivityIds = courseActivities
            .Select(a => a.Id)
            .Concat(researchActivityIds)
            .Distinct()
            .ToList();

        var materials = allActivityIds.Count > 0
            ? await unitOfWork.Materials.GetAllAsync(
                m => allActivityIds.Contains(m.ActivityId) && !m.IsDeleted)
            : new List<Material>();

        var activitiesByCourseId = courseActivities
            .GroupBy(a => a.CourseId)
            .ToDictionary(
                g => g.Key,
                g => g.OrderBy(a => a.ActivityOrder).ToList());

        var coursesByModuleId = courses
            .GroupBy(c => c.ModuleId)
            .ToDictionary(
                g => g.Key,
                g => g.OrderBy(c => c.Name).ToList());

        var milestoneLinksByMilestoneId = milestoneActivityLinks
            .GroupBy(rma => rma.ResearchMilestoneId)
            .ToDictionary(
                g => g.Key,
                g => g.OrderBy(rma => rma.DisplayOrder).ToList());

        var activitiesById = courseActivities
            .Concat(researchActivities)
            .GroupBy(a => a.Id)
            .ToDictionary(g => g.Key, g => g.First());

        var milestonesByModuleId = milestones
            .GroupBy(m => m.ModuleId)
            .ToDictionary(
                g => g.Key,
                g => g.OrderBy(m => m.MilestoneOrder).ToList());

        var activityModuleMap = new Dictionary<Guid, Guid>();
        var orderedActivitiesByCourseId = new Dictionary<Guid, List<Guid>>();
        var orderedActivitiesByMilestoneId = new Dictionary<Guid, List<Guid>>();
        var globalActivityOrder = new List<Guid>();

        foreach (var module in modules)
        {
            if (module.ModuleType == ModuleType.Research)
            {
                if (!milestonesByModuleId.TryGetValue(module.Id, out var moduleMilestones))
                {
                    continue;
                }

                foreach (var milestone in moduleMilestones)
                {
                    var orderedIds = new List<Guid>();
                    if (milestoneLinksByMilestoneId.TryGetValue(milestone.Id, out var links))
                    {
                        foreach (var link in links)
                        {
                            if (!activitiesById.ContainsKey(link.ActivityId))
                            {
                                continue;
                            }

                            orderedIds.Add(link.ActivityId);
                            activityModuleMap[link.ActivityId] = module.Id;
                            globalActivityOrder.Add(link.ActivityId);
                        }
                    }

                    orderedActivitiesByMilestoneId[milestone.Id] = orderedIds;
                }
            }
            else if (coursesByModuleId.TryGetValue(module.Id, out var moduleCourses))
            {
                foreach (var course in moduleCourses)
                {
                    var orderedIds = new List<Guid>();
                    if (activitiesByCourseId.TryGetValue(course.Id, out var moduleActivities))
                    {
                        foreach (var activity in moduleActivities)
                        {
                            orderedIds.Add(activity.Id);
                            activityModuleMap[activity.Id] = module.Id;
                            globalActivityOrder.Add(activity.Id);
                        }
                    }

                    orderedActivitiesByCourseId[course.Id] = orderedIds;
                }
            }
        }

        return new ProgramCurriculumTreeSnapshot
        {
            Program = program,
            Modules = modules,
            CoursesByModuleId = coursesByModuleId,
            ActivitiesByCourseId = activitiesByCourseId,
            MilestonesByModuleId = milestonesByModuleId,
            LinksByMilestoneId = milestoneLinksByMilestoneId,
            ActivitiesById = activitiesById,
            MaterialsByActivityId = materials.ToDictionary(m => m.ActivityId),
            ActivityModuleMap = activityModuleMap,
            GlobalActivityOrder = globalActivityOrder,
            OrderedActivitiesByCourseId = orderedActivitiesByCourseId,
            OrderedActivitiesByMilestoneId = orderedActivitiesByMilestoneId,
        };
    }
}
