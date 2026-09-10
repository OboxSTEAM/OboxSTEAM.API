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
                Code = module.Code,
                Name = module.Name,
                Order = module.ModuleOrder,
                Type = module.ModuleType.ToString(),
                ModuleType = module.ModuleType.ToString(),
                PrerequisiteModuleId = module.PrerequisiteModuleId,
                IsMandatory = module.IsMandatory,
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
                    var courseSnap = new CourseSnapshot
                    {
                        Id = course.Id,
                        Code = course.Code,
                        Name = course.Name,
                        Order = course.CourseOrder,
                        Description = course.Description,
                        Activities = [],
                    };
                    moduleSnap.Courses.Add(courseSnap);

                    if (tree.ActivitiesByCourseId.TryGetValue(course.Id, out var activities))
                    {
                        foreach (var activity in activities)
                        {
                            var activitySnap = MapActivity(activity, course.Id, null, activity.ActivityOrder, tree);
                            courseSnap.Activities.Add(activitySnap);
                            moduleSnap.Activities.Add(activitySnap);
                            if (activitySnap.Material != null)
                            {
                                moduleSnap.Materials.Add(activitySnap.Material);
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
                    var milestoneSnap = new MilestoneSnapshot
                    {
                        Id = milestone.Id,
                        Code = milestone.Code,
                        Title = milestone.Title,
                        Order = milestone.MilestoneOrder,
                        IsCapstone = milestone.IsCapstone,
                        Description = milestone.Description,
                        AssignmentId = milestone.AssignmentId,
                        Activities = [],
                    };
                    moduleSnap.Milestones.Add(milestoneSnap);

                    if (tree.AssignmentsById.TryGetValue(milestone.AssignmentId, out var deliverable))
                    {
                        milestoneSnap.Assignment = MapAssignment(deliverable, "ResearchMilestone");
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
                            var activitySnap = MapActivity(activity, null, milestone.Id, order, tree);
                            milestoneSnap.ActivityIds.Add(activity.Id);
                            milestoneSnap.Activities.Add(activitySnap);
                            moduleSnap.Activities.Add(activitySnap);
                            if (activitySnap.Material != null)
                            {
                                moduleSnap.Materials.Add(activitySnap.Material);
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
            Program = new ProgramSnapshot
            {
                Id = tree.Program.Id,
                Name = tree.Program.Name,
                Code = tree.Program.Code,
                Description = tree.Program.Description,
                SkillsGained = tree.Program.SkillsGained,
                FrameworkVersionId = tree.Program.FrameworkVersionId,
            },
            Modules = modules,
        };
    }

    private static ActivitySnapshot MapActivity(
        Activity activity,
        Guid? courseId,
        Guid? milestoneId,
        int order,
        ProgramCurriculumTreeSnapshot tree)
    {
        MaterialSnapshot? materialSnapshot = null;
        if (tree.MaterialsByActivityId.TryGetValue(activity.Id, out var material)
            && material is { IsDeleted: false })
        {
            materialSnapshot = new MaterialSnapshot
            {
                Id = material.Id,
                ActivityId = activity.Id,
                Title = material.Title,
                MaterialType = material.MaterialType.ToString(),
                Type = material.MaterialType.ToString(),
                FileName = GetFileName(material.FileUrl),
                Url = material.FileUrl,
                FileSizeBytes = material.FileSizeBytes,
            };
        }

        return new ActivitySnapshot
        {
            Id = activity.Id,
            CourseId = courseId,
            MilestoneId = milestoneId,
            Name = activity.Name,
            Type = activity.ActivityType.ToString(),
            ActivityType = activity.ActivityType.ToString(),
            Order = order,
            Description = activity.Description,
            DurationMinutes = activity.DurationMinutes,
            RequireQrCheckin = activity.RequireQrCheckin,
            RequireMediaEvidence = activity.RequireMediaEvidence,
            Material = materialSnapshot,
        };
    }

    private static string? GetFileName(string? fileUrl)
    {
        if (string.IsNullOrWhiteSpace(fileUrl))
        {
            return null;
        }

        return Uri.TryCreate(fileUrl, UriKind.Absolute, out var uri)
            ? Path.GetFileName(uri.LocalPath)
            : Path.GetFileName(fileUrl);
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
        var prevItems = Flatten(previous)
            .GroupBy(i => (i.TargetType, i.Id))
            .ToDictionary(g => g.Key, g => g.First());
        var currItems = Flatten(current)
            .GroupBy(i => (i.TargetType, i.Id))
            .ToDictionary(g => g.Key, g => g.First());

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

            foreach (var change in DiffFields(prior, item))
            {
                result.Modified.Add(new SubmissionChangeItemDto
                {
                    TargetType = item.TargetType,
                    Id = item.Id,
                    Label = item.Label,
                    Field = change.Field,
                    Before = change.Before,
                    After = change.After,
                    Detail = change.Detail,
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
            Code = assignment.Code,
            ModuleId = assignment.ModuleId,
            CourseId = assignment.CourseId,
            Title = assignment.Title,
            Scope = scope,
            Description = assignment.Description,
            AssignmentType = assignment.AssignmentType.ToString(),
            MaxPoints = assignment.MaxPoints,
            PassScore = assignment.PassScore,
            IsRequiredForModulePass = assignment.IsRequiredForModulePass,
            TimeLimitMinutes = assignment.TimeLimitMinutes,
            MaxAttempts = assignment.MaxAttempts,
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
                new Dictionary<string, string?>
                {
                    ["code"] = module.Code,
                    ["type"] = string.IsNullOrWhiteSpace(module.ModuleType) ? module.Type : module.ModuleType,
                    ["prerequisiteModuleId"] = module.PrerequisiteModuleId?.ToString(),
                    ["isMandatory"] = module.IsMandatory.ToString(),
                    ["learningOutcomes"] = string.Join("|", module.LearningOutcomes ?? []),
                });

            foreach (var course in module.Courses)
            {
                yield return new FlatItem(
                    ProgramAdvisoryTargetType.Course,
                    course.Id,
                    course.Name,
                    course.Order,
                    new Dictionary<string, string?>
                    {
                        ["code"] = course.Code,
                        ["description"] = course.Description,
                    });

                foreach (var activity in course.Activities)
                {
                    yield return ToActivityItem(activity);
                    if (activity.Material != null)
                    {
                        yield return ToMaterialItem(activity.Material);
                    }
                }
            }

            if (module.Courses.All(c => c.Activities.Count == 0))
            {
                foreach (var activity in module.Activities)
                {
                    yield return ToActivityItem(activity);
                }
            }

            if (module.Courses.All(c => c.Activities.All(a => a.Material == null)))
            {
                foreach (var material in module.Materials)
                {
                    yield return ToMaterialItem(material);
                }
            }

            foreach (var assignment in module.Assignments)
            {
                yield return new FlatItem(
                    ProgramAdvisoryTargetType.Assignment,
                    assignment.Id,
                    assignment.Title,
                    0,
                    new Dictionary<string, string?>
                    {
                        ["code"] = assignment.Code,
                        ["moduleId"] = assignment.ModuleId.ToString(),
                        ["courseId"] = assignment.CourseId?.ToString(),
                        ["scope"] = assignment.Scope,
                        ["description"] = assignment.Description,
                        ["assignmentType"] = assignment.AssignmentType,
                        ["maxPoints"] = assignment.MaxPoints.ToString(),
                        ["passScore"] = assignment.PassScore.ToString(),
                        ["availableFrom"] = assignment.AvailableFrom?.ToString("O"),
                        ["dueAt"] = assignment.DueAt?.ToString("O"),
                    });
            }

            foreach (var milestone in module.Milestones)
            {
                yield return new FlatItem(
                    ProgramAdvisoryTargetType.ResearchMilestone,
                    milestone.Id,
                    milestone.Title,
                    milestone.Order,
                    new Dictionary<string, string?>
                    {
                        ["code"] = milestone.Code,
                        ["isCapstone"] = milestone.IsCapstone.ToString(),
                        ["description"] = milestone.Description,
                        ["assignmentId"] = milestone.AssignmentId.ToString(),
                        ["activityIds"] = string.Join("|", milestone.ActivityIds),
                    });

                foreach (var activity in milestone.Activities)
                {
                    yield return ToActivityItem(activity);
                    if (activity.Material != null)
                    {
                        yield return ToMaterialItem(activity.Material);
                    }
                }
            }
        }
    }

    private static FlatItem ToActivityItem(ActivitySnapshot activity)
        => new(
            ProgramAdvisoryTargetType.Activity,
            activity.Id,
            activity.Name,
            activity.Order,
            new Dictionary<string, string?>
            {
                ["type"] = string.IsNullOrWhiteSpace(activity.ActivityType) ? activity.Type : activity.ActivityType,
                ["description"] = activity.Description,
                ["durationMinutes"] = activity.DurationMinutes?.ToString(),
                ["requireQrCheckin"] = activity.RequireQrCheckin.ToString(),
                ["requireMediaEvidence"] = activity.RequireMediaEvidence.ToString(),
            });

    private static FlatItem ToMaterialItem(MaterialSnapshot material)
        => new(
            ProgramAdvisoryTargetType.Material,
            material.Id,
            material.Title,
            0,
            new Dictionary<string, string?>
            {
                ["materialType"] = string.IsNullOrWhiteSpace(material.MaterialType) ? material.Type : material.MaterialType,
                ["fileName"] = material.FileName,
                ["url"] = material.Url,
            });

    private static IEnumerable<FlatFieldChange> DiffFields(FlatItem prior, FlatItem current)
    {
        var fields = prior.Fields.Keys
            .Concat(current.Fields.Keys)
            .Distinct(StringComparer.Ordinal);
        foreach (var field in fields)
        {
            prior.Fields.TryGetValue(field, out var before);
            current.Fields.TryGetValue(field, out var after);
            if (string.Equals(before, after, StringComparison.Ordinal))
            {
                continue;
            }

            yield return new FlatFieldChange(
                field,
                before,
                after,
                $"{before ?? "(empty)"} → {after ?? "(empty)"}");
        }

        if (!string.Equals(prior.Label, current.Label, StringComparison.Ordinal))
        {
            yield return new FlatFieldChange(
                "label",
                prior.Label,
                current.Label,
                $"{prior.Label} → {current.Label}");
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
        IReadOnlyDictionary<string, string?> Fields);

    private sealed record FlatFieldChange(string Field, string? Before, string? After, string Detail);

    public sealed class CurriculumSnapshotDocument
    {
        public Guid ProgramId { get; set; }

        public string? ProgramName { get; set; }

        public ProgramSnapshot Program { get; set; } = new();

        public List<ModuleSnapshot> Modules { get; set; } = [];
    }

    public sealed class ModuleSnapshot
    {
        public Guid Id { get; set; }

        public string Code { get; set; } = null!;

        public string Name { get; set; } = null!;

        public int Order { get; set; }

        public string Type { get; set; } = null!;

        public string ModuleType { get; set; } = null!;

        public Guid? PrerequisiteModuleId { get; set; }

        public bool IsMandatory { get; set; }

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

        public string Code { get; set; } = null!;

        public string Name { get; set; } = null!;

        public int Order { get; set; }

        public string? Description { get; set; }

        public List<ActivitySnapshot> Activities { get; set; } = [];
    }

    public sealed class ActivitySnapshot
    {
        public Guid Id { get; set; }

        public Guid? CourseId { get; set; }

        public Guid? MilestoneId { get; set; }

        public string Name { get; set; } = null!;

        public string Type { get; set; } = null!;

        public string ActivityType { get; set; } = null!;

        public int Order { get; set; }

        public string? Description { get; set; }

        public int? DurationMinutes { get; set; }

        public bool RequireQrCheckin { get; set; }

        public bool RequireMediaEvidence { get; set; }

        public MaterialSnapshot? Material { get; set; }
    }

    public sealed class MaterialSnapshot
    {
        public Guid Id { get; set; }

        public Guid ActivityId { get; set; }

        public string Title { get; set; } = null!;

        public string Type { get; set; } = null!;

        public string MaterialType { get; set; } = null!;

        public string? FileName { get; set; }

        public string? Url { get; set; }

        public long? FileSizeBytes { get; set; }
    }

    public sealed class AssignmentSnapshot
    {
        public Guid Id { get; set; }

        public string Code { get; set; } = null!;

        public Guid ModuleId { get; set; }

        public Guid? CourseId { get; set; }

        public string Title { get; set; } = null!;

        public string Scope { get; set; } = null!;

        public string? Description { get; set; }

        public string? AssignmentType { get; set; }

        public int MaxPoints { get; set; }

        public decimal PassScore { get; set; }

        public bool IsRequiredForModulePass { get; set; }

        public int? TimeLimitMinutes { get; set; }

        public int MaxAttempts { get; set; }

        public DateTime? AvailableFrom { get; set; }

        public DateTime? DueAt { get; set; }
    }

    public sealed class MilestoneSnapshot
    {
        public Guid Id { get; set; }

        public string Code { get; set; } = null!;

        public string Title { get; set; } = null!;

        public int Order { get; set; }

        public bool IsCapstone { get; set; }

        public string? Description { get; set; }

        public Guid AssignmentId { get; set; }

        public AssignmentSnapshot? Assignment { get; set; }

        public List<ActivitySnapshot> Activities { get; set; } = [];

        public List<Guid> ActivityIds { get; set; } = [];
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

    public sealed class ProgramSnapshot
    {
        public Guid Id { get; set; }

        public string Name { get; set; } = null!;

        public string Code { get; set; } = null!;

        public string? Description { get; set; }

        public string? SkillsGained { get; set; }

        public Guid? FrameworkVersionId { get; set; }
    }
}
