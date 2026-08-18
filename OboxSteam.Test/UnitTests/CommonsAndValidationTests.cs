using OboxSteam.Application.Commons;
using OboxSteam.Application.DTOs.EnrollmentDTO;
using OboxSteam.Application.DTOs.PortfolioDTO;
using OboxSteam.Application.Exceptions;
using OboxSteam.Application.Validation;
using OboxSteam.Domain.Entities;
using OboxSteam.Domain.Enums;

namespace OboxSteam.Test.UnitTests;

public sealed class CommonsAndValidationTests
{
    private static readonly DateTime FixedNow = new(2026, 7, 29, 8, 0, 0, DateTimeKind.Utc);

    // ── ScheduleTimeValidator ─────────────────────────────────────────────────

    [Fact]
    public void ScheduleTimeValidator_ValidateFutureRange_Throws_WhenStartInPast()
    {
        Assert.Throws<BadRequestException>(() =>
            ScheduleTimeValidator.ValidateFutureRange(
                FixedNow.AddHours(-1),
                FixedNow.AddHours(2),
                utcNow: FixedNow));
    }

    [Fact]
    public void ScheduleTimeValidator_ValidateFutureRange_Throws_WhenEndInPast()
    {
        Assert.Throws<BadRequestException>(() =>
            ScheduleTimeValidator.ValidateFutureRange(
                FixedNow.AddHours(1),
                FixedNow.AddMinutes(-1),
                utcNow: FixedNow));
    }

    [Fact]
    public void ScheduleTimeValidator_ValidateFutureRange_Throws_WhenEndBeforeStart()
    {
        Assert.Throws<BadRequestException>(() =>
            ScheduleTimeValidator.ValidateFutureRange(
                FixedNow.AddHours(3),
                FixedNow.AddHours(2),
                utcNow: FixedNow));
    }

    [Fact]
    public void ScheduleTimeValidator_ValidateFutureRange_Skips_WhenEitherNull()
    {
        ScheduleTimeValidator.ValidateFutureRange(null, FixedNow.AddHours(2), utcNow: FixedNow);
        ScheduleTimeValidator.ValidateFutureRange(FixedNow.AddHours(1), null, utcNow: FixedNow);
    }

    [Fact]
    public void ScheduleTimeValidator_ValidateBothRequired_Throws_WhenMissing()
    {
        Assert.Throws<BadRequestException>(() =>
            ScheduleTimeValidator.ValidateBothRequired(null, FixedNow));
        Assert.Throws<BadRequestException>(() =>
            ScheduleTimeValidator.ValidateBothRequired(FixedNow, null));
    }

    [Fact]
    public void ScheduleTimeValidator_ValidateRequiredIfAnyProvided_Throws_WhenOnlyOneProvided()
    {
        Assert.Throws<BadRequestException>(() =>
            ScheduleTimeValidator.ValidateRequiredIfAnyProvided(FixedNow, null));
        Assert.Throws<BadRequestException>(() =>
            ScheduleTimeValidator.ValidateRequiredIfAnyProvided(null, FixedNow));
    }

    // ── PortfolioThemeValidator ───────────────────────────────────────────────

    [Fact]
    public void PortfolioThemeValidator_ValidateTheme_AcceptsValidConfig()
    {
        var theme = new ThemeConfigDto
        {
            PrimaryColor = "#AABBCC",
            SecondaryColor = "#FFF",
            AccentColor = "#12345678",
            BackgroundImageUrl = "https://cdn.example.com/bg.png",
            HeadingFontFamily = "Inter",
            FontFamily = "Roboto",
            SettingsJson = "{\"radius\":8}",
        };

        PortfolioThemeValidator.ValidateTheme(theme);

        Assert.Equal("{\"radius\":8}", theme.SettingsJson);
    }

    [Fact]
    public void PortfolioThemeValidator_Throws_ForInvalidHexColor()
    {
        Assert.Throws<BadRequestException>(() =>
            PortfolioThemeValidator.ValidateHexColor("red", "PrimaryColor"));
    }

    [Fact]
    public void PortfolioThemeValidator_Throws_ForInvalidUrl()
    {
        Assert.Throws<BadRequestException>(() =>
            PortfolioThemeValidator.ValidateOptionalUrl("ftp://bad", "BackgroundImageUrl", 500));
        Assert.Throws<BadRequestException>(() =>
            PortfolioThemeValidator.ValidateOptionalUrl(new string('a', 501), "BackgroundImageUrl", 500));
    }

    [Fact]
    public void PortfolioThemeValidator_Throws_ForInvalidJsonSettings()
    {
        Assert.Throws<BadRequestException>(() =>
            PortfolioThemeValidator.NormalizeOptionalJsonObject("[1,2]", "SettingsJson", 100));
        Assert.Throws<BadRequestException>(() =>
            PortfolioThemeValidator.NormalizeOptionalJsonObject("{bad", "SettingsJson", 100));
        Assert.Throws<BadRequestException>(() =>
            PortfolioThemeValidator.NormalizeOptionalJsonObject(new string('x', 2001), "SettingsJson", 2000));
    }

    [Fact]
    public void PortfolioThemeValidator_NormalizeOptionalJsonObject_ReturnsNull_ForWhitespace()
    {
        Assert.Null(PortfolioThemeValidator.NormalizeOptionalJsonObject("   ", "SettingsJson", 100));
    }

    [Fact]
    public void PortfolioThemeValidator_Throws_WhenFontFamilyTooLong()
    {
        var theme = new ThemeConfigDto
        {
            PrimaryColor = "#000000",
            SecondaryColor = "#111111",
            AccentColor = "#222222",
            FontFamily = new string('f', 101),
        };

        Assert.Throws<BadRequestException>(() => PortfolioThemeValidator.ValidateTheme(theme));
    }

    // ── ActivityResumeStateHelper ───────────────────────────────────────────

    [Fact]
    public void ActivityResumeStateHelper_RoundTripsJson()
    {
        var state = new ActivityResumeStateDto { Kind = "video", PositionSeconds = 42 };
        var json = ActivityResumeStateHelper.Serialize(state);
        var restored = ActivityResumeStateHelper.Deserialize(json);

        Assert.NotNull(restored);
        Assert.Equal("video", restored!.Kind);
        Assert.Equal(42, restored.PositionSeconds);
    }

    [Fact]
    public void ActivityResumeStateHelper_Deserialize_ReturnsNull_ForEmpty()
    {
        Assert.Null(ActivityResumeStateHelper.Deserialize(null));
        Assert.Null(ActivityResumeStateHelper.Deserialize("  "));
    }

    [Theory]
    [InlineData("video", null, null, null)]
    [InlineData("video", -1, null, null)]
    [InlineData("pdf", null, 0, null)]
    [InlineData("doc", null, null, 1.5)]
    [InlineData("slides", null, null, null)]
    public void ActivityResumeStateHelper_ValidateResumeState_Throws_ForInvalid(
        string kind, int? position, int? page, double? scrollRatio)
    {
        var state = new ActivityResumeStateDto
        {
            Kind = kind,
            PositionSeconds = position,
            Page = page,
            ScrollRatio = scrollRatio,
        };

        Assert.Throws<BadRequestException>(() => ActivityResumeStateHelper.ValidateResumeState(state));
    }

    [Fact]
    public void ActivityResumeStateHelper_ParseCompletionSource_MapsKnownValues()
    {
        Assert.Equal(CompletionSource.Manual, ActivityResumeStateHelper.ParseCompletionSource("manual"));
        Assert.Equal(CompletionSource.Video, ActivityResumeStateHelper.ParseCompletionSource("VIDEO"));
        Assert.Null(ActivityResumeStateHelper.ParseCompletionSource(null));
        Assert.Throws<BadRequestException>(() => ActivityResumeStateHelper.ParseCompletionSource("auto"));
    }

    [Fact]
    public void ActivityResumeStateHelper_ToApiString_MapsKnownValues()
    {
        Assert.Equal("manual", ActivityResumeStateHelper.ToApiString(CompletionSource.Manual));
        Assert.Equal("reading", ActivityResumeStateHelper.ToApiString(CompletionSource.Reading));
        Assert.Null(ActivityResumeStateHelper.ToApiString(null));
    }

    // ── QuizQuestionDrawHelper / QuizOperationValidator ───────────────────────

    [Fact]
    public void QuizOperationValidator_ValidateDrawInput_Throws_WhenEmptyOrInvalidCount()
    {
        Assert.Throws<BadRequestException>(() =>
            QuizOperationValidator.ValidateDrawInput([], 5));
        Assert.Throws<BadRequestException>(() =>
            QuizOperationValidator.ValidateDrawInput([new BankQuestion { Id = Guid.NewGuid() }], 0));
    }

    [Fact]
    public void QuizOperationValidator_ValidateGradingInput_Throws_WhenInvalid()
    {
        Assert.Throws<BadRequestException>(() =>
            QuizOperationValidator.ValidateGradingInput(
                new Assignment { MaxPoints = 0 },
                [new QuizQuestion { Id = Guid.NewGuid() }]));
        Assert.Throws<BadRequestException>(() =>
            QuizOperationValidator.ValidateGradingInput(
                new Assignment { MaxPoints = 10 },
                []));
    }

    [Fact]
    public void QuizQuestionDrawHelper_Draw_RespectsDifficultyPoolsAndShuffle()
    {
        var questions = Enumerable.Range(1, 10).Select(i => new BankQuestion
        {
            Id = Guid.NewGuid(),
            DifficultyLevel = i <= 3 ? 1 : i <= 7 ? 3 : 5,
        }).ToList();

        var drawn = QuizQuestionDrawHelper.Draw(questions, drawCount: 6, easyPercent: 50, mediumPercent: 30, hardPercent: 20, allowShuffle: true);

        Assert.Equal(6, drawn.Count);
        Assert.Equal(6, drawn.Select(q => q.Id).Distinct().Count());
    }

    [Fact]
    public void QuizQuestionDrawHelper_Draw_Backfills_WhenTierPoolsAreSmall()
    {
        var easyOnly = Enumerable.Range(0, 3).Select(_ => new BankQuestion
        {
            Id = Guid.NewGuid(),
            DifficultyLevel = 1,
        }).ToList();

        var drawn = QuizQuestionDrawHelper.Draw(easyOnly, drawCount: 3, easyPercent: 10, mediumPercent: 80, hardPercent: 10, allowShuffle: false);

        Assert.Equal(3, drawn.Count);
    }

    // ── CurriculumStatusHelper ────────────────────────────────────────────────

    [Fact]
    public void CurriculumStatusHelper_IsActivityCompleted_UsesProgressDone()
    {
        var activityId = Guid.NewGuid();
        var progress = new Dictionary<Guid, ActivityProgress>
        {
            [activityId] = new() { ActivityStatus = ActivityStatus.Done },
        };

        Assert.True(CurriculumStatusHelper.IsActivityCompleted(activityId, progress));
        Assert.False(CurriculumStatusHelper.IsActivityCompleted(activityId, new Dictionary<Guid, ActivityProgress>()));
    }

    [Fact]
    public void CurriculumStatusHelper_ModuleLockReason_ReflectsPrerequisite()
    {
        var prereqId = Guid.NewGuid();
        var moduleId = Guid.NewGuid();
        var module = new Module
        {
            Id = moduleId,
            PrerequisiteModuleId = prereqId,
        };
        var modulesById = new Dictionary<Guid, Module>
        {
            [prereqId] = new() { Id = prereqId, Name = "Basics" },
            [moduleId] = module,
        };

        var reason = CurriculumStatusHelper.GetModuleLockReason(module, new Dictionary<Guid, ModuleEnrollment>(), modulesById);

        Assert.Contains("Basics", reason);
    }

    [Fact]
    public void CurriculumStatusHelper_IsActivitySequentiallyAccessible_RequiresPriorActivities()
    {
        var a1 = Guid.NewGuid();
        var a2 = Guid.NewGuid();
        var courseId = Guid.NewGuid();
        var snapshot = new ProgramCurriculumTreeSnapshot
        {
            OrderedActivitiesByCourseId = new Dictionary<Guid, List<Guid>>
            {
                [courseId] = [a1, a2],
            },
            ActivityModuleMap = new Dictionary<Guid, Guid>
            {
                [a1] = Guid.NewGuid(),
                [a2] = Guid.NewGuid(),
            },
        };

        Assert.False(CurriculumStatusHelper.IsActivitySequentiallyAccessible(a2, snapshot, _ => false));
        Assert.True(CurriculumStatusHelper.IsActivitySequentiallyAccessible(a2, snapshot, _ => true));
    }

    [Fact]
    public void CurriculumStatusHelper_FindNextAndCurrentActivity_WalksGlobalOrder()
    {
        var a1 = Guid.NewGuid();
        var a2 = Guid.NewGuid();
        var snapshot = new ProgramCurriculumTreeSnapshot
        {
            GlobalActivityOrder = [a1, a2],
            ActivityModuleMap = new Dictionary<Guid, Guid> { [a1] = Guid.NewGuid(), [a2] = Guid.NewGuid() },
        };

        Assert.Equal(a2, CurriculumStatusHelper.FindNextActivityId(snapshot, a1, _ => true, _ => false));
        Assert.Equal(a1, CurriculumStatusHelper.FindCurrentActivityId(snapshot, _ => true, id => id == a2));
        Assert.Null(CurriculumStatusHelper.FindNextActivityId(snapshot, a2, _ => true, _ => false));
    }

    [Fact]
    public void CurriculumStatusHelper_AssignmentAccessibility_UsesCourseModuleAndResearchPaths()
    {
        var courseId = Guid.NewGuid();
        var moduleId = Guid.NewGuid();
        var activityId = Guid.NewGuid();
        var snapshot = new ProgramCurriculumTreeSnapshot
        {
            OrderedActivitiesByCourseId = new Dictionary<Guid, List<Guid>> { [courseId] = [activityId] },
            ActivityModuleMap = new Dictionary<Guid, Guid> { [activityId] = moduleId },
            LinksByMilestoneId = new Dictionary<Guid, List<ResearchMilestoneActivity>>(),
            AssignmentsById = new Dictionary<Guid, Assignment>(),
        };

        var courseAssignment = new Assignment { CourseId = courseId };
        Assert.False(CurriculumStatusHelper.IsAssignmentAccessible(
            courseAssignment, moduleId, snapshot, _ => false));

        var moduleAssignment = new Assignment { CourseId = null };
        Assert.True(CurriculumStatusHelper.IsAssignmentAccessible(
            moduleAssignment, moduleId, snapshot, _ => true));

        var milestone = new ResearchMilestone { Id = Guid.NewGuid() };
        Assert.True(CurriculumStatusHelper.IsAssignmentAccessible(
            new Assignment(), moduleId, snapshot, _ => true, researchMilestone: milestone));
    }
}
