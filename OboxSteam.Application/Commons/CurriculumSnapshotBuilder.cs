using System.Text.Json;
using System.Text.Json.Serialization;
using OboxSteam.Domain.Entities;

namespace OboxSteam.Application.Commons;

public static class CurriculumSnapshotBuilder
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
            AllowShuffle = assignment.AllowShuffle,
            QuestionBankId = assignment.QuestionBankId,
            QuestionCount = assignment.QuestionCount,
            ShuffleOptions = assignment.ShuffleOptions,
            EasyPercent = assignment.EasyPercent,
            MediumPercent = assignment.MediumPercent,
            HardPercent = assignment.HardPercent,
        };

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

        public bool AllowShuffle { get; set; }

        public Guid? QuestionBankId { get; set; }

        public int? QuestionCount { get; set; }

        public bool ShuffleOptions { get; set; }

        public int EasyPercent { get; set; }

        public int MediumPercent { get; set; }

        public int HardPercent { get; set; }
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
