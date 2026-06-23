using OboxSteam.Application.DTOs.ProgramDTO;
using OboxSteam.Domain.Entities;
using OboxSteam.Domain.Enums;

namespace OboxSteam.Application.Commons;

/// <summary>
/// Maps a loaded curriculum tree snapshot to static program curriculum DTOs.
/// </summary>
public static class ProgramCurriculumTreeMapper
{
    public static ProgramCurriculumDto ToProgramCurriculumDto(ProgramCurriculumTreeSnapshot snapshot)
    {
        var moduleDtos = snapshot.Modules.Select(module =>
        {
            var moduleDto = new ProgramCurriculumModuleDto
            {
                ModuleId = module.Id,
                ModuleName = module.Name,
                ModuleOrder = module.ModuleOrder,
                ModuleType = module.ModuleType,
                PrerequisiteModuleId = module.PrerequisiteModuleId,
            };

            if (module.ModuleType == ModuleType.Research)
            {
                moduleDto.Milestones = snapshot.MilestonesByModuleId.TryGetValue(module.Id, out var moduleMilestones)
                    ? moduleMilestones.Select(milestone => new ProgramCurriculumMilestoneDto
                    {
                        MilestoneId = milestone.Id,
                        MilestoneName = milestone.Title,
                        MilestoneOrder = milestone.MilestoneOrder,
                        Activities = snapshot.LinksByMilestoneId.TryGetValue(milestone.Id, out var links)
                            ? links
                                .Select(link => snapshot.ActivitiesById.GetValueOrDefault(link.ActivityId))
                                .Where(activity => activity != null)
                                .Select(activity => MapCurriculumActivity(activity!, snapshot.MaterialsByActivityId))
                                .ToList()
                            : [],
                    }).ToList()
                    : [];
            }
            else if (snapshot.CoursesByModuleId.TryGetValue(module.Id, out var moduleCourses))
            {
                var courseOrder = 1;
                moduleDto.Courses = moduleCourses.Select(course => new ProgramCurriculumCourseDto
                {
                    CourseId = course.Id,
                    CourseName = course.Name,
                    CourseOrder = courseOrder++,
                    Activities = snapshot.ActivitiesByCourseId.TryGetValue(course.Id, out var moduleActivities)
                        ? moduleActivities
                            .Select(activity => MapCurriculumActivity(activity, snapshot.MaterialsByActivityId))
                            .ToList()
                        : [],
                }).ToList();
            }

            return moduleDto;
        }).ToList();

        return new ProgramCurriculumDto
        {
            ProgramId = snapshot.Program.Id,
            ProgramName = snapshot.Program.Name,
            Modules = moduleDtos,
        };
    }

    public static ProgramCurriculumActivityDto MapCurriculumActivity(
        Activity activity,
        IReadOnlyDictionary<Guid, Material> materialsByActivityId)
    {
        materialsByActivityId.TryGetValue(activity.Id, out var material);

        return new ProgramCurriculumActivityDto
        {
            ActivityId = activity.Id,
            ActivityName = activity.Name,
            ActivityOrder = activity.ActivityOrder,
            ActivityType = activity.ActivityType,
            Material = material == null
                ? null
                : new ProgramCurriculumMaterialDto
                {
                    MaterialId = material.Id,
                    MaterialName = material.Title,
                    MaterialType = material.MaterialType,
                },
        };
    }
}
