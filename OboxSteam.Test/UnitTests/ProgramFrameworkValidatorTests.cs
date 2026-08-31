using OboxSteam.Application.Commons;
using OboxSteam.Application.Exceptions;
using OboxSteam.Application.Validation;
using OboxSteam.Domain.Entities;
using OboxSteam.Domain.Enums;
using OboxSteam.Test.Helpers;

namespace OboxSteam.Test.UnitTests;

public sealed class ProgramFrameworkValidatorTests
{
    private readonly Guid _programId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private readonly Guid _frameworkId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private readonly Guid _moduleId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private readonly Guid _researchModuleId = Guid.Parse("34343434-3434-3434-3434-343434343434");
    private readonly Guid _courseId = Guid.Parse("44444444-4444-4444-4444-444444444444");
    private readonly Guid _offlineId = Guid.Parse("55555555-5555-5555-5555-555555555555");
    private readonly Guid _liveId = Guid.Parse("56565656-5656-5656-5656-565656565656");
    private readonly Guid _milestoneId = Guid.Parse("57575757-5757-5757-5757-575757575757");

    private readonly InMemoryUnitOfWork _db = new();

    [Fact]
    public void CollectRuleFailures_SkipsNullRules()
    {
        var framework = new ProgramFramework { Name = "Open", Category = ProgramCategory.Technology };
        var snapshot = EmptySnapshot();

        var errors = ProgramFrameworkValidator.CollectRuleFailures(framework, snapshot);

        Assert.Empty(errors);
    }

    [Fact]
    public void CollectRuleFailures_RequireFinalAssessmentFalse_DoesNotRequireCapstone()
    {
        var framework = new ProgramFramework
        {
            Name = "C#",
            Category = ProgramCategory.Technology,
            RequireFinalAssessment = false,
        };

        var errors = ProgramFrameworkValidator.CollectRuleFailures(framework, EmptySnapshot());

        Assert.Empty(errors);
    }

    [Fact]
    public void CollectRuleFailures_JoinsEveryFailingRule()
    {
        var framework = new ProgramFramework
        {
            Name = "Strict",
            Category = ProgramCategory.Technology,
            MinModules = 2,
            MinOfflineSessions = 1,
            MinLiveSessions = 1,
            RequireFinalAssessment = true,
        };

        var errors = ProgramFrameworkValidator.CollectRuleFailures(framework, EmptySnapshot());

        Assert.Equal(4, errors.Count);
        Assert.Contains(errors, e => e.Contains("module", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(errors, e => e.Contains("Offline", StringComparison.Ordinal));
        Assert.Contains(errors, e => e.Contains("LiveOnline", StringComparison.Ordinal));
        Assert.Contains(errors, e => e.Contains("capstone", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void CollectRuleFailures_RequireFinalAssessmentTrue_PassesWithCapstone()
    {
        var capstone = new ResearchMilestone
        {
            Id = _milestoneId,
            ModuleId = _researchModuleId,
            Code = "RML-01",
            Title = "Capstone",
            IsCapstone = true,
        };
        var snapshot = EmptySnapshot();
        snapshot.Modules.Add(new Module
        {
            Id = _researchModuleId,
            ProgramId = _programId,
            Code = "MOD-R",
            Name = "Research",
            ModuleType = ModuleType.Research,
        });
        snapshot.MilestonesByModuleId[_researchModuleId] = [capstone];

        var framework = new ProgramFramework
        {
            Name = "C#",
            Category = ProgramCategory.Technology,
            RequireFinalAssessment = true,
        };

        Assert.Empty(ProgramFrameworkValidator.CollectRuleFailures(framework, snapshot));
    }

    [Fact]
    public async Task ValidateForSubmitAsync_SkipsWhenNoFramework()
    {
        SeedProgram();

        await ProgramFrameworkValidator.ValidateForSubmitAsync(_db, _programId);
    }

    [Fact]
    public async Task ValidateForSubmitAsync_ThrowsJoinedMessage_WhenMultipleRulesFail()
    {
        SeedProgram(frameworkId: _frameworkId);
        _db.ProgramFrameworks.Seed(new ProgramFramework
        {
            Id = _frameworkId,
            ExpertId = Guid.NewGuid(),
            Name = "Strict",
            Category = ProgramCategory.Technology,
            MinModules = 2,
            MinOfflineSessions = 1,
            RequireFinalAssessment = true,
        });

        var ex = await Assert.ThrowsAsync<BadRequestException>(
            () => ProgramFrameworkValidator.ValidateForSubmitAsync(_db, _programId));

        Assert.Contains("module", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Offline", ex.Message, StringComparison.Ordinal);
        Assert.Contains("capstone", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ValidateForSubmitAsync_PassesWhenCurriculumMeetsDefinedRules()
    {
        SeedFullCurriculum(_frameworkId);
        _db.ProgramFrameworks.Seed(new ProgramFramework
        {
            Id = _frameworkId,
            ExpertId = Guid.NewGuid(),
            Name = "C#",
            Category = ProgramCategory.Technology,
            MinModules = 1,
            MinOfflineSessions = 1,
            MinLiveSessions = 1,
            RequireFinalAssessment = true,
        });

        await ProgramFrameworkValidator.ValidateForSubmitAsync(_db, _programId);
    }

    [Fact]
    public void ValidatePositiveConstraint_RejectsZero()
    {
        var ex = Assert.Throws<BadRequestException>(
            () => ProgramFrameworkValidator.ValidatePositiveConstraint("MinModules", 0));
        Assert.Contains("greater than 0", ex.Message);
    }

    private ProgramCurriculumTreeSnapshot EmptySnapshot() => new()
    {
        Program = new Program
        {
            Id = _programId,
            Code = "PRG-001",
            Name = "Program",
            Category = ProgramCategory.Technology,
        },
    };

    private void SeedProgram(Guid? frameworkId = null)
    {
        _db.Programs.Seed(new Program
        {
            Id = _programId,
            Code = "PRG-001",
            Name = "Program",
            Category = ProgramCategory.Technology,
            FrameworkId = frameworkId,
            Modules = [],
            IsDeleted = false,
        });
    }

    private void SeedFullCurriculum(Guid frameworkId)
    {
        var theory = new Module
        {
            Id = _moduleId,
            ProgramId = _programId,
            Code = "MOD-T",
            Name = "Theory",
            ModuleType = ModuleType.Theory,
            ModuleOrder = 1,
            IsDeleted = false,
        };
        var research = new Module
        {
            Id = _researchModuleId,
            ProgramId = _programId,
            Code = "MOD-R",
            Name = "Research",
            ModuleType = ModuleType.Research,
            ModuleOrder = 2,
            IsDeleted = false,
        };

        _db.Programs.Seed(new Program
        {
            Id = _programId,
            Code = "PRG-001",
            Name = "Program",
            Category = ProgramCategory.Technology,
            FrameworkId = frameworkId,
            Modules = [theory, research],
            IsDeleted = false,
        });
        _db.Modules.Seed(theory, research);
        _db.Courses.Seed(new Course
        {
            Id = _courseId,
            ModuleId = _moduleId,
            Code = "CRS-001",
            Name = "Course",
            IsDeleted = false,
        });
        _db.Activities.Seed(
            new Activity
            {
                Id = _offlineId,
                CourseId = _courseId,
                Code = "ACT-OFF",
                Name = "Lab",
                ActivityType = ActivityType.Offline,
                IsDeleted = false,
            },
            new Activity
            {
                Id = _liveId,
                CourseId = _courseId,
                Code = "ACT-LIVE",
                Name = "Live",
                ActivityType = ActivityType.LiveOnline,
                IsDeleted = false,
            });
        _db.ResearchMilestones.Seed(new ResearchMilestone
        {
            Id = _milestoneId,
            ModuleId = _researchModuleId,
            AssignmentId = Guid.NewGuid(),
            Code = "RML-01",
            Title = "Capstone",
            IsCapstone = true,
            IsDeleted = false,
        });
    }
}
