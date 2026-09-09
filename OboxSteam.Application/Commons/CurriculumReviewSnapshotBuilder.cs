using System.Text.Json;
using System.Text.Json.Serialization;
using OboxSteam.Application.DTOs.ProgramAdvisoryDTO;
using OboxSteam.Domain.Entities;
using OboxSteam.Domain.Enums;

namespace OboxSteam.Application.Commons;

public static class CurriculumReviewSnapshotBuilder
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = false,
    };

    public static string BuildCurriculumSnapshotJson(ProgramCurriculumTreeSnapshot tree)
    {
        var snapshot = BuildCurriculumSnapshot(tree);
        return JsonSerializer.Serialize(snapshot, JsonOptions);
    }

    public static string BuildRubricSnapshotJson(IReadOnlyList<FrameworkRubricCriterion> criteria)
    {
        var rows = criteria
            .OrderBy(c => c.DisplayOrder)
            .ThenBy(c => c.Name)
            .Select(c => new RubricCriterionSnapshot
            {
                Id = c.Id,
                Name = c.Name,
                Description = c.Description,
                EvidenceGuidance = c.EvidenceGuidance,
                MaxScore = c.MaxScore,
                DisplayOrder = c.DisplayOrder,
            })
            .ToList();
        return JsonSerializer.Serialize(rows, JsonOptions);
    }

    public static CurriculumSnapshotDocument BuildCurriculumSnapshot(ProgramCurriculumTreeSnapshot tree)
    {
        var modules = new List<ModuleSnapshot>();
        foreach (var module in tree.Modules)
        {
            var moduleSnap = new ModuleSnapshot
            {
                Id = module.Id,
                Name = module.Name,
                Order = module.ModuleOrder,
                Type = module.ModuleType.ToString(),
                LearningOutcomes = module.LearningOutcomes,
                Courses = [],
                Activities = [],
                Materials = [],
                Assignments = [],
                Milestones = [],
            };

            if (tree.CoursesByModuleId.TryGetValue(module.Id, out var courses))
            {
                foreach (var course in courses)
                {
                    moduleSnap.Courses.Add(new CourseSnapshot
                    {
                        Id = course.Id,
                        Name = course.Name,
                        Order = course.CourseOrder,
                        Description = course.Description,
                    });

                    if (tree.ActivitiesByCourseId.TryGetValue(course.Id, out var activities))
                    {
                        foreach (var activity in activities)
                        {
                            moduleSnap.Activities.Add(new ActivitySnapshot
                            {
                                Id = activity.Id,
                                CourseId = course.Id,
                                Name = activity.Name,
                                Type = activity.ActivityType.ToString(),
                                Order = activity.ActivityOrder,
                                Description = activity.Description,
                            });

                            if (tree.MaterialsByActivityId.TryGetValue(activity.Id, out var material)
                                && material is { IsDeleted: false })
                            {
                                moduleSnap.Materials.Add(new MaterialSnapshot
                                {
                                    Id = material.Id,
                                    ActivityId = activity.Id,
                                    Title = material.Title,
                                    Type = material.MaterialType.ToString(),
                                    Url = material.FileUrl,
                                });
                            }
                        }
                    }

                    if (tree.AssignmentsByCourseId.TryGetValue(course.Id, out var courseAssignments))
                    {
                        foreach (var assignment in courseAssignments)
                        {
                            moduleSnap.Assignments.Add(MapAssignment(assignment, "Course"));
                        }
                    }
                }
            }

            if (tree.ModuleScopedAssignmentsByModuleId.TryGetValue(module.Id, out var moduleAssignments))
            {
                foreach (var assignment in moduleAssignments)
                {
                    moduleSnap.Assignments.Add(MapAssignment(assignment, "Module"));
                }
            }

            if (tree.MilestonesByModuleId.TryGetValue(module.Id, out var milestones))
            {
                foreach (var milestone in milestones)
                {
                    moduleSnap.Milestones.Add(new MilestoneSnapshot
                    {
                        Id = milestone.Id,
                        Title = milestone.Title,
                        Order = milestone.MilestoneOrder,
                        IsCapstone = milestone.IsCapstone,
                        Description = milestone.Description,
                    });

                    if (tree.AssignmentsById.TryGetValue(milestone.AssignmentId, out var deliverable))
                    {
                        moduleSnap.Assignments.Add(MapAssignment(deliverable, "ResearchMilestone"));
                    }

                    if (tree.OrderedActivitiesByMilestoneId.TryGetValue(milestone.Id, out var activityIds))
                    {
                        var order = 0;
                        foreach (var activityId in activityIds)
                        {
                            if (!tree.ActivitiesById.TryGetValue(activityId, out var activity))
                            {
                                continue;
                            }

                            order++;
                            moduleSnap.Activities.Add(new ActivitySnapshot
                            {
                                Id = activity.Id,
                                MilestoneId = milestone.Id,
                                Name = activity.Name,
                                Type = activity.ActivityType.ToString(),
                                Order = order,
                                Description = activity.Description,
                            });

                            if (tree.MaterialsByActivityId.TryGetValue(activity.Id, out var material)
                                && material is { IsDeleted: false })
                            {
                                moduleSnap.Materials.Add(new MaterialSnapshot
                                {
                                    Id = material.Id,
                                    ActivityId = activity.Id,
                                    Title = material.Title,
                                    Type = material.MaterialType.ToString(),
                                    Url = material.FileUrl,
                                });
                            }
                        }
                    }
                }
            }

            modules.Add(moduleSnap);
        }

        return new CurriculumSnapshotDocument
        {
            ProgramId = tree.Program.Id,
            ProgramName = tree.Program.Name,
            Modules = modules,
        };
    }

    public static SubmissionChangesDto Diff(
        Guid submissionId,
        Guid? previousSubmissionId,
        string? previousJson,
        string currentJson)
    {
        var result = new SubmissionChangesDto
        {
            SubmissionId = submissionId,
            PreviousSubmissionId = previousSubmissionId,
        };

        if (string.IsNullOrWhiteSpace(previousJson))
        {
            var currentOnly = Deserialize(currentJson);
            foreach (var item in Flatten(currentOnly))
            {
                result.Added.Add(ToChange(item));
            }

            return result;
        }

        var previous = Deserialize(previousJson);
        var current = Deserialize(currentJson);
        var prevItems = Flatten(previous).ToDictionary(i => (i.TargetType, i.Id));
        var currItems = Flatten(current).ToDictionary(i => (i.TargetType, i.Id));

        foreach (var (key, item) in currItems)
        {
            if (!prevItems.TryGetValue(key, out var prior))
            {
                result.Added.Add(ToChange(item));
                continue;
            }

            if (item.Order != prior.Order)
            {
                result.Reordered.Add(new SubmissionChangeItemDto
                {
                    TargetType = item.TargetType,
                    Id = item.Id,
                    Label = item.Label,
                    Field = "order",
                    Detail = $"{prior.Order} → {item.Order}",
                });
            }

            foreach (var (field, detail) in DiffFields(prior, item))
            {
                result.Modified.Add(new SubmissionChangeItemDto
                {
                    TargetType = item.TargetType,
                    Id = item.Id,
                    Label = item.Label,
                    Field = field,
                    Detail = detail,
                });
            }
        }

        foreach (var (key, item) in prevItems)
        {
            if (!currItems.ContainsKey(key))
            {
                result.Removed.Add(ToChange(item));
            }
        }

        return result;
    }

    public static CurriculumSnapshotDocument? TryDeserialize(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        return Deserialize(json);
    }

    private static CurriculumSnapshotDocument Deserialize(string json)
        => JsonSerializer.Deserialize<CurriculumSnapshotDocument>(json, JsonOptions)
           ?? new CurriculumSnapshotDocument();

    private static AssignmentSnapshot MapAssignment(Assignment assignment, string scope)
        => new()
        {
            Id = assignment.Id,
            Title = assignment.Title,
            Scope = scope,
            Description = assignment.Description,
            AssignmentType = assignment.AssignmentType.ToString(),
        };

    private static IEnumerable<FlatItem> Flatten(CurriculumSnapshotDocument doc)
    {
        foreach (var module in doc.Modules)
        {
            yield return new FlatItem(
                ProgramAdvisoryTargetType.Module,
                module.Id,
                module.Name,
                module.Order,
                module.Type,
                null,
                string.Join("|", module.LearningOutcomes ?? []));

            foreach (var course in module.Courses)
            {
                yield return new FlatItem(
                    ProgramAdvisoryTargetType.Course,
                    course.Id,
                    course.Name,
                    course.Order,
                    null,
                    course.Description,
                    null);
            }

            foreach (var activity in module.Activities)
            {
                yield return new FlatItem(
                    ProgramAdvisoryTargetType.Activity,
                    activity.Id,
                    activity.Name,
                    activity.Order,
                    activity.Type,
                    activity.Description,
                    null);
            }

            foreach (var material in module.Materials)
            {
                yield return new FlatItem(
                    ProgramAdvisoryTargetType.Material,
                    material.Id,
                    material.Title,
                    0,
                    material.Type,
                    material.Url,
                    null);
            }

            foreach (var assignment in module.Assignments)
            {
                yield return new FlatItem(
                    ProgramAdvisoryTargetType.Assignment,
                    assignment.Id,
                    assignment.Title,
                    0,
                    assignment.Scope,
                    assignment.Description,
                    assignment.AssignmentType);
            }

            foreach (var milestone in module.Milestones)
            {
                yield return new FlatItem(
                    ProgramAdvisoryTargetType.ResearchMilestone,
                    milestone.Id,
                    milestone.Title,
                    milestone.Order,
                    milestone.IsCapstone ? "Capstone" : null,
                    milestone.Description,
                    null);
            }
        }
    }

    private static IEnumerable<(string Field, string Detail)> DiffFields(FlatItem prior, FlatItem current)
    {
        if (!string.Equals(prior.TypeOrScope, current.TypeOrScope, StringComparison.Ordinal))
        {
            yield return ("type", $"{prior.TypeOrScope} → {current.TypeOrScope}");
        }

        if (!string.Equals(prior.Description, current.Description, StringComparison.Ordinal))
        {
            yield return ("description", "Changed");
        }

        if (!string.Equals(prior.Extra, current.Extra, StringComparison.Ordinal))
        {
            yield return ("detail", "Changed");
        }

        if (!string.Equals(prior.Label, current.Label, StringComparison.Ordinal))
        {
            yield return ("label", $"{prior.Label} → {current.Label}");
        }
    }

    private static SubmissionChangeItemDto ToChange(FlatItem item)
        => new()
        {
            TargetType = item.TargetType,
            Id = item.Id,
            Label = item.Label,
        };

    private sealed record FlatItem(
        ProgramAdvisoryTargetType TargetType,
        Guid Id,
        string Label,
        int Order,
        string? TypeOrScope,
        string? Description,
        string? Extra);

    public sealed class CurriculumSnapshotDocument
    {
        public Guid ProgramId { get; set; }

        public string? ProgramName { get; set; }

        public List<ModuleSnapshot> Modules { get; set; } = [];
    }

    public sealed class ModuleSnapshot
    {
        public Guid Id { get; set; }

        public string Name { get; set; } = null!;

        public int Order { get; set; }

        public string Type { get; set; } = null!;

        public string[]? LearningOutcomes { get; set; }

        public List<CourseSnapshot> Courses { get; set; } = [];

        public List<ActivitySnapshot> Activities { get; set; } = [];

        public List<MaterialSnapshot> Materials { get; set; } = [];

        public List<AssignmentSnapshot> Assignments { get; set; } = [];

        public List<MilestoneSnapshot> Milestones { get; set; } = [];
    }

    public sealed class CourseSnapshot
    {
        public Guid Id { get; set; }

        public string Name { get; set; } = null!;

        public int Order { get; set; }

        public string? Description { get; set; }
    }

    public sealed class ActivitySnapshot
    {
        public Guid Id { get; set; }

        public Guid? CourseId { get; set; }

        public Guid? MilestoneId { get; set; }

        public string Name { get; set; } = null!;

        public string Type { get; set; } = null!;

        public int Order { get; set; }

        public string? Description { get; set; }
    }

    public sealed class MaterialSnapshot
    {
        public Guid Id { get; set; }

        public Guid ActivityId { get; set; }

        public string Title { get; set; } = null!;

        public string Type { get; set; } = null!;

        public string? Url { get; set; }
    }

    public sealed class AssignmentSnapshot
    {
        public Guid Id { get; set; }

        public string Title { get; set; } = null!;

        public string Scope { get; set; } = null!;

        public string? Description { get; set; }

        public string? AssignmentType { get; set; }
    }

    public sealed class MilestoneSnapshot
    {
        public Guid Id { get; set; }

        public string Title { get; set; } = null!;

        public int Order { get; set; }

        public bool IsCapstone { get; set; }

        public string? Description { get; set; }
    }

    public sealed class RubricCriterionSnapshot
    {
        public Guid Id { get; set; }

        public string Name { get; set; } = null!;

        public string? Description { get; set; }

        public string? EvidenceGuidance { get; set; }

        public int MaxScore { get; set; }

        public int DisplayOrder { get; set; }
    }
}
