using OboxSteam.Domain.Entities;

namespace OboxSteam.Application.Commons;

/// <summary>
/// Loaded curriculum tree data for a program, shared by static and enrollment-scoped curriculum APIs.
/// </summary>
public sealed class ProgramCurriculumTreeSnapshot
{
    public Program Program { get; init; } = null!;

    public List<Module> Modules { get; init; } = [];

    public Dictionary<Guid, List<Course>> CoursesByModuleId { get; init; } = new();

    public Dictionary<Guid, List<Activity>> ActivitiesByCourseId { get; init; } = new();

    public Dictionary<Guid, List<ResearchMilestone>> MilestonesByModuleId { get; init; } = new();

    public Dictionary<Guid, List<ResearchMilestoneActivity>> LinksByMilestoneId { get; init; } = new();

    public Dictionary<Guid, Activity> ActivitiesById { get; init; } = new();

    public Dictionary<Guid, Material> MaterialsByActivityId { get; init; } = new();

    /// <summary>Activity ID to owning module ID.</summary>
    public Dictionary<Guid, Guid> ActivityModuleMap { get; init; } = new();

    /// <summary>Global program order of activity IDs (modules → courses/milestones → activities).</summary>
    public List<Guid> GlobalActivityOrder { get; init; } = [];

    /// <summary>Ordered activity IDs within each course (key = courseId).</summary>
    public Dictionary<Guid, List<Guid>> OrderedActivitiesByCourseId { get; init; } = new();

    /// <summary>Ordered activity IDs within each research milestone (key = milestoneId).</summary>
    public Dictionary<Guid, List<Guid>> OrderedActivitiesByMilestoneId { get; init; } = new();
}
