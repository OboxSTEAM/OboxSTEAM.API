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

    /// <summary>Course-scoped assignments for non-research modules (key = courseId).</summary>
    public Dictionary<Guid, List<Assignment>> AssignmentsByCourseId { get; init; } = new();

    /// <summary>Module-scoped assignments (CourseId null) for non-research modules (key = moduleId).</summary>
    public Dictionary<Guid, List<Assignment>> ModuleScopedAssignmentsByModuleId { get; init; } = new();

    /// <summary>All loaded assignments by id (includes research milestone deliverables).</summary>
    public Dictionary<Guid, Assignment> AssignmentsById { get; init; } = new();
}
