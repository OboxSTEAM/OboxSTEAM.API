using OboxSteam.Application.Commons;
using OboxSteam.Application.Exceptions;
using OboxSteam.Application.Validation;
using OboxSteam.Domain.Entities;
using OboxSteam.Domain.Enums;

namespace OboxSteam.Test.UnitTests;

public sealed class ProgramFrameworkValidatorTests
{
    private readonly Guid _programId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    [Fact]
    public void Evaluate_SkipsRulesThatAreOff()
    {
        Assert.Empty(FrameworkRuleEvaluator.Evaluate(new ProgramFrameworkVersion(), null, EmptySnapshot(), 0));
    }

    [Fact]
    public void Evaluate_OriginalFourRules_KeepCodesAndExpectedValues()
    {
        var version = new ProgramFrameworkVersion
        {
            MinModules = 2, MinOfflineSessions = 1, MinLiveSessions = 1,
            RequireCapstoneResearchMilestone = true,
        };

        var checks = FrameworkRuleEvaluator.Evaluate(version, null, EmptySnapshot(), 0);

        Assert.Equal(
            ["MinModules", "MinOfflineSessions", "MinLiveSessions", "RequireCapstoneResearchMilestone"],
            checks.Select(c => c.Code));
        Assert.All(checks, c => Assert.False(c.Passed));
        Assert.Equal("2", checks[0].Expected);
        Assert.Equal("At least 1", checks[3].Expected);
    }

    [Fact]
    public void Evaluate_MaxModules_LinksModulesBeyondMax()
    {
        var snapshot = EmptySnapshot();
        var m1 = AddModule(snapshot, "M1", ModuleType.Theory);
        var m2 = AddModule(snapshot, "M2", ModuleType.Theory);
        var m3 = AddModule(snapshot, "M3", ModuleType.Theory);

        var check = Single(new ProgramFrameworkVersion { MaxModules = 1 }, snapshot);

        Assert.False(check.Passed);
        Assert.Equal("3", check.Actual);
        Assert.Equal([m2.Id, m3.Id], check.AffectedCurriculumLinks.Select(l => l.Id));
        Assert.DoesNotContain(check.AffectedCurriculumLinks, l => l.Id == m1.Id);
    }

    [Fact]
    public void Evaluate_CoursesPerModule_IgnoresResearchModules()
    {
        var snapshot = EmptySnapshot();
        var theory = AddModule(snapshot, "Theory", ModuleType.Theory);
        AddModule(snapshot, "Research", ModuleType.Research);
        AddCourse(snapshot, theory);

        var check = Single(new ProgramFrameworkVersion { MinCoursesPerModule = 1, MaxCoursesPerModule = 2 }, snapshot);

        Assert.True(check.Passed);
        Assert.Equal("1-2 per module", check.Expected);
        Assert.Equal("1 per module", check.Actual);
    }

    [Fact]
    public void Evaluate_TotalHours_ComparesInMinutes()
    {
        var snapshot = EmptySnapshot();
        var course = AddCourse(snapshot, AddModule(snapshot, "M", ModuleType.Experiential));
        AddActivity(snapshot, course, ActivityType.Offline, 90);
        AddActivity(snapshot, course, ActivityType.Offline, 30);

        var pass = Single(new ProgramFrameworkVersion { MinTotalHours = 2 }, snapshot);
        var fail = Single(new ProgramFrameworkVersion { MaxTotalHours = 1 }, snapshot);

        Assert.True(pass.Passed);
        Assert.Equal("2 h", pass.Actual);
        Assert.False(fail.Passed);
    }

    [Fact]
    public void Evaluate_ActivityDurationRules_LinkOffendingActivities()
    {
        var snapshot = EmptySnapshot();
        var course = AddCourse(snapshot, AddModule(snapshot, "M", ModuleType.Experiential));
        var tooLong = AddActivity(snapshot, course, ActivityType.Offline, 200);
        var missing = AddActivity(snapshot, course, ActivityType.SelfPaced, null);
        var zero = AddActivity(snapshot, course, ActivityType.SelfPaced, 0);

        var checks = FrameworkRuleEvaluator.Evaluate(
            new ProgramFrameworkVersion { MaxActivityMinutes = 120, RequireActivityDuration = true },
            null,
            snapshot,
            0);

        var max = checks.Single(c => c.Code == "MaxActivityDuration");
        Assert.Equal([tooLong.Id], max.AffectedCurriculumLinks.Select(l => l.Id));
        var set = checks.Single(c => c.Code == "ActivityDurationSet");
        Assert.Equal([missing.Id, zero.Id], set.AffectedCurriculumLinks.Select(l => l.Id));
    }

    [Fact]
    public void Evaluate_Ratios_ShareOfActivities_ZeroWhenNoActivities()
    {
        var snapshot = EmptySnapshot();
        var course = AddCourse(snapshot, AddModule(snapshot, "M", ModuleType.Experiential));
        AddActivity(snapshot, course, ActivityType.Offline, 60);
        AddActivity(snapshot, course, ActivityType.SelfPaced, 60);
        AddActivity(snapshot, course, ActivityType.SelfPaced, 60);
        AddActivity(snapshot, course, ActivityType.LiveOnline, 60);

        var checks = FrameworkRuleEvaluator.Evaluate(
            new ProgramFrameworkVersion { MinOfflineRatioPercent = 25, MinLiveRatioPercent = 30 },
            null,
            snapshot,
            0);

        Assert.True(checks.Single(c => c.Code == "OfflineRatio").Passed);
        var live = checks.Single(c => c.Code == "LiveRatio");
        Assert.False(live.Passed);
        Assert.Equal("25%", live.Actual);

        var empty = Single(new ProgramFrameworkVersion { MinOfflineRatioPercent = 0 }, EmptySnapshot());
        Assert.True(empty.Passed);
        Assert.Equal("0%", empty.Actual);
    }

    [Fact]
    public void Evaluate_AssignmentRules()
    {
        var snapshot = EmptySnapshot();
        var withAssignment = AddModule(snapshot, "With", ModuleType.Theory);
        var without = AddModule(snapshot, "Without", ModuleType.Theory);
        var valid = AddAssignment(snapshot, withAssignment, "A1", passScore: 5, maxPoints: 10);
        var invalid = AddAssignment(snapshot, withAssignment, "A2", passScore: 12, maxPoints: 10);
        _ = valid;

        var checks = FrameworkRuleEvaluator.Evaluate(
            new ProgramFrameworkVersion { RequireAssignmentPerModule = true, RequireAssignmentPassScore = true },
            null,
            snapshot,
            0);

        var perModule = checks.Single(c => c.Code == "AssignmentPerModule");
        Assert.Equal([without.Id], perModule.AffectedCurriculumLinks.Select(l => l.Id));
        var passScore = checks.Single(c => c.Code == "AssignmentPassScore");
        Assert.Equal([invalid.Id], passScore.AffectedCurriculumLinks.Select(l => l.Id));
    }

    [Fact]
    public void Evaluate_MaterialsPerActivity_OnlySelfPaced()
    {
        var snapshot = EmptySnapshot();
        var course = AddCourse(snapshot, AddModule(snapshot, "M", ModuleType.Theory));
        var withMaterial = AddActivity(snapshot, course, ActivityType.SelfPaced, 30);
        var noMaterial = AddActivity(snapshot, course, ActivityType.SelfPaced, 30);
        AddActivity(snapshot, course, ActivityType.Offline, 30);
        snapshot.MaterialsByActivityId[withMaterial.Id] = new Material
        {
            Id = Guid.NewGuid(), ActivityId = withMaterial.Id, Title = "Pack",
        };

        var check = Single(new ProgramFrameworkVersion { MinMaterialsPerActivity = 1 }, snapshot);

        Assert.False(check.Passed);
        Assert.Equal([noMaterial.Id], check.AffectedCurriculumLinks.Select(l => l.Id));
    }

    [Fact]
    public void Evaluate_ProgramLevelRules_HaveNoLinks()
    {
        var snapshot = EmptySnapshot();
        snapshot.Program.Description = "Short";
        snapshot.Program.Category = ProgramCategory.Science;

        var checks = FrameworkRuleEvaluator.Evaluate(
            new ProgramFrameworkVersion
            {
                RequireCategoryMatch = true, MinDescriptionLength = 10, MinSkillsGained = 2, RequireThumbnail = true,
            },
            ProgramCategory.Technology,
            snapshot,
            programSkillCount: 3);

        Assert.Equal(["CategoryMatch", "DescriptionLength", "SkillsGained", "ThumbnailSet"], checks.Select(c => c.Code));
        Assert.Equal([false, false, true, false], checks.Select(c => c.Passed));
        Assert.All(checks, c => Assert.Empty(c.AffectedCurriculumLinks));
    }

    [Fact]
    public void ValidateRules_AllowsZero()
    {
        ProgramFrameworkValidator.ValidateRules(new ProgramFrameworkVersion
        {
            MinModules = 0, MaxModules = 0, MinOfflineRatioPercent = 0,
        });
    }

    [Fact]
    public void ValidateRules_ReportsEveryViolation()
    {
        var error = Assert.Throws<BadRequestException>(() => ProgramFrameworkValidator.ValidateRules(
            new ProgramFrameworkVersion
            {
                MinModules = -1,
                MinTotalHours = 10, MaxTotalHours = 5,
                MinOfflineRatioPercent = 70, MinLiveRatioPercent = 40,
            }));

        Assert.Equal(ProgramFrameworkValidator.RulesInvalidCode, error.ErrorCode);
        Assert.Contains("MinModules must be 0 or greater", error.Message, StringComparison.Ordinal);
        Assert.Contains("MinTotalHours must be less than or equal to MaxTotalHours", error.Message, StringComparison.Ordinal);
        Assert.Contains("must be at most 100", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ValidateRules_RejectsRatioAbove100()
    {
        var error = Assert.Throws<BadRequestException>(() => ProgramFrameworkValidator.ValidateRules(
            new ProgramFrameworkVersion { MinLiveRatioPercent = 101 }));

        Assert.Equal(ProgramFrameworkValidator.RulesInvalidCode, error.ErrorCode);
    }

    private static Application.DTOs.ProgramAdvisoryDTO.FrameworkCheckItemDto Single(
        ProgramFrameworkVersion version,
        ProgramCurriculumTreeSnapshot snapshot)
        => Assert.Single(FrameworkRuleEvaluator.Evaluate(version, null, snapshot, 0));

    private static Module AddModule(ProgramCurriculumTreeSnapshot snapshot, string name, ModuleType type)
    {
        var module = new Module
        {
            Id = Guid.NewGuid(), Code = $"MOD-{name}", Name = name, ModuleType = type,
            ModuleOrder = snapshot.Modules.Count + 1, ProgramId = snapshot.Program.Id,
        };
        snapshot.Modules.Add(module);
        return module;
    }

    private static Course AddCourse(ProgramCurriculumTreeSnapshot snapshot, Module module)
    {
        var course = new Course { Id = Guid.NewGuid(), Code = $"CRS-{Guid.NewGuid():N}", ModuleId = module.Id, Name = "Course" };
        if (!snapshot.CoursesByModuleId.TryGetValue(module.Id, out var courses))
        {
            courses = [];
            snapshot.CoursesByModuleId[module.Id] = courses;
        }

        courses.Add(course);
        return course;
    }

    private static Activity AddActivity(
        ProgramCurriculumTreeSnapshot snapshot,
        Course course,
        ActivityType type,
        int? durationMinutes)
    {
        var activity = new Activity
        {
            Id = Guid.NewGuid(), Code = $"ACT-{Guid.NewGuid():N}", CourseId = course.Id,
            Name = $"Activity {snapshot.ActivitiesById.Count + 1}", ActivityType = type,
            DurationMinutes = durationMinutes,
        };
        snapshot.ActivitiesById[activity.Id] = activity;
        snapshot.GlobalActivityOrder.Add(activity.Id);
        return activity;
    }

    private static Assignment AddAssignment(
        ProgramCurriculumTreeSnapshot snapshot,
        Module module,
        string code,
        decimal passScore,
        int maxPoints)
    {
        var assignment = new Assignment
        {
            Id = Guid.NewGuid(), Code = code, Title = code, ModuleId = module.Id,
            PassScore = passScore, MaxPoints = maxPoints,
        };
        snapshot.AssignmentsById[assignment.Id] = assignment;
        return assignment;
    }

    private ProgramCurriculumTreeSnapshot EmptySnapshot() => new()
    {
        Program = new Program
        {
            Id = _programId, Code = "PRG", Name = "Program", Category = ProgramCategory.Technology,
        },
    };
}
