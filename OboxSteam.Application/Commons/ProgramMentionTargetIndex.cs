using OboxSteam.Application.DTOs.CurriculumChangeDTO;
using OboxSteam.Application.DTOs.ProgramAdvisoryDTO;
using OboxSteam.Domain.Entities;
using OboxSteam.Domain.Enums;

namespace OboxSteam.Application.Commons;

/// <summary>Every mentionable component of a program, flattened in curriculum tree order.</summary>
public sealed class ProgramMentionTargetIndex
{
    private readonly Dictionary<(ProgramAdvisoryTargetType, Guid), MentionTargetDto> _byKey;

    private ProgramMentionTargetIndex(List<MentionTargetDto> targets)
    {
        Targets = targets;
        _byKey = targets.ToDictionary(t => (t.TargetType, t.TargetId));
    }

    public IReadOnlyList<MentionTargetDto> Targets { get; }

    public MentionTargetDto? Find(ProgramAdvisoryTargetType targetType, Guid targetId)
        => _byKey.GetValueOrDefault((targetType, targetId));

    public static ProgramMentionTargetIndex Build(ProgramCurriculumTreeSnapshot tree)
    {
        ArgumentNullException.ThrowIfNull(tree);
        var builder = new Builder();
        var program = tree.Program;
        var programSegment = Segment(ProgramAdvisoryTargetType.Program, program.Id, program.Name);
        builder.Add(programSegment, program.Code, [], null, null, null);

        foreach (var module in tree.Modules)
        {
            var moduleSegment = Segment(ProgramAdvisoryTargetType.Module, module.Id, module.Name);
            List<CurriculumPathSegmentDto> modulePath = [programSegment];
            builder.Add(moduleSegment, module.Code, modulePath, module.Id, null, null);
            List<CurriculumPathSegmentDto> moduleChildPath = [programSegment, moduleSegment];

            if (module.ModuleType == ModuleType.Research)
            {
                foreach (var milestone in tree.MilestonesByModuleId.GetValueOrDefault(module.Id) ?? [])
                {
                    var milestoneSegment = Segment(ProgramAdvisoryTargetType.ResearchMilestone, milestone.Id, milestone.Title);
                    builder.Add(milestoneSegment, milestone.Code, moduleChildPath, module.Id, null, null);
                    List<CurriculumPathSegmentDto> milestoneChildPath = [programSegment, moduleSegment, milestoneSegment];
                    foreach (var link in tree.LinksByMilestoneId.GetValueOrDefault(milestone.Id) ?? [])
                    {
                        if (tree.ActivitiesById.TryGetValue(link.ActivityId, out var activity))
                        {
                            AddActivity(builder, tree, activity, milestoneChildPath, module.Id, null);
                        }
                    }

                    if (tree.AssignmentsById.TryGetValue(milestone.AssignmentId, out var deliverable))
                    {
                        AddAssignment(builder, deliverable, milestoneChildPath, module.Id);
                    }
                }
            }
            else
            {
                foreach (var course in tree.CoursesByModuleId.GetValueOrDefault(module.Id) ?? [])
                {
                    var courseSegment = Segment(ProgramAdvisoryTargetType.Course, course.Id, course.Name);
                    builder.Add(courseSegment, course.Code, moduleChildPath, module.Id, course.Id, null);
                    List<CurriculumPathSegmentDto> courseChildPath = [programSegment, moduleSegment, courseSegment];
                    foreach (var activity in tree.ActivitiesByCourseId.GetValueOrDefault(course.Id) ?? [])
                    {
                        AddActivity(builder, tree, activity, courseChildPath, module.Id, course.Id);
                    }

                    foreach (var assignment in tree.AssignmentsByCourseId.GetValueOrDefault(course.Id) ?? [])
                    {
                        AddAssignment(builder, assignment, courseChildPath, module.Id);
                    }
                }
            }

            foreach (var assignment in tree.AssignmentsById.Values
                         .Where(a => a.ModuleId == module.Id)
                         .OrderBy(a => a.Code))
            {
                AddAssignment(builder, assignment, moduleChildPath, module.Id);
            }
        }

        return new ProgramMentionTargetIndex(builder.Targets);
    }

    private static void AddActivity(
        Builder builder,
        ProgramCurriculumTreeSnapshot tree,
        Activity activity,
        List<CurriculumPathSegmentDto> path,
        Guid moduleId,
        Guid? courseId)
    {
        var activitySegment = Segment(ProgramAdvisoryTargetType.Activity, activity.Id, activity.Name);
        if (!builder.Add(activitySegment, activity.Code, path, moduleId, courseId, activity.Id))
        {
            return;
        }

        if (tree.MaterialsByActivityId.TryGetValue(activity.Id, out var material) && !material.IsDeleted)
        {
            builder.Add(
                Segment(ProgramAdvisoryTargetType.Material, material.Id, material.Title),
                null,
                [.. path, activitySegment],
                moduleId,
                courseId,
                activity.Id);
        }
    }

    private static void AddAssignment(
        Builder builder,
        Assignment assignment,
        List<CurriculumPathSegmentDto> path,
        Guid moduleId)
        => builder.Add(
            Segment(ProgramAdvisoryTargetType.Assignment, assignment.Id, assignment.Title),
            assignment.Code,
            path,
            moduleId,
            assignment.CourseId,
            null);

    private static CurriculumPathSegmentDto Segment(ProgramAdvisoryTargetType targetType, Guid targetId, string? label)
        => new() { TargetType = targetType, TargetId = targetId, Label = label ?? string.Empty };

    private sealed class Builder
    {
        private readonly HashSet<(ProgramAdvisoryTargetType, Guid)> _seen = [];

        public List<MentionTargetDto> Targets { get; } = [];

        /// <summary>Returns false when the target was already emitted (shared activities, deliverables).</summary>
        public bool Add(
            CurriculumPathSegmentDto segment,
            string? code,
            List<CurriculumPathSegmentDto> path,
            Guid? moduleId,
            Guid? courseId,
            Guid? activityId)
        {
            if (!_seen.Add((segment.TargetType, segment.TargetId)))
            {
                return false;
            }

            Targets.Add(new MentionTargetDto
            {
                TargetType = segment.TargetType,
                TargetId = segment.TargetId,
                Label = segment.Label,
                Code = code,
                Path = [.. path],
                ModuleId = moduleId,
                CourseId = courseId,
                ActivityId = activityId,
                Order = Targets.Count,
            });
            return true;
        }
    }
}
