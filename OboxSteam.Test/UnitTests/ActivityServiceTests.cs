using Microsoft.Extensions.Logging.Abstractions;
using OboxSteam.Application.DTOs.ActivityDTO;
using OboxSteam.Application.Exceptions;
using OboxSteam.Application.Services;
using OboxSteam.Domain.Entities;
using OboxSteam.Domain.Enums;
using OboxSteam.Test.Helpers;

namespace OboxSteam.Test.UnitTests;

public sealed class ActivityServiceTests
{
    private readonly Guid _programId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private readonly Guid _moduleId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private readonly Guid _experientialModuleId = Guid.Parse("34343434-3434-3434-3434-343434343434");
    private readonly Guid _courseId = Guid.Parse("44444444-4444-4444-4444-444444444444");
    private readonly Guid _otherCourseId = Guid.Parse("45454545-4545-4545-4545-454545454545");
    private readonly Guid _activityId = Guid.Parse("55555555-5555-5555-5555-555555555555");
    private readonly Guid _activity2Id = Guid.Parse("56565656-5656-5656-5656-565656565656");
    private readonly Guid _activity3Id = Guid.Parse("57575757-5757-5757-5757-575757575757");
    private readonly Guid _materialId = Guid.Parse("66666666-6666-6666-6666-666666666666");

    private readonly InMemoryUnitOfWork _db = new();

    private ActivityService CreateSut() =>
        new(_db, NullLogger<ActivityService>.Instance);

    private static (DateTime Start, DateTime End) FutureSchedule()
    {
        var start = DateTime.UtcNow.AddDays(3);
        return (start, start.AddHours(2));
    }

    private void SeedProgram()
    {
        _db.Programs.Seed(new Program
        {
            Id = _programId,
            Code = "PRG-001",
            Name = "Program",
            Category = ProgramCategory.Technology,
            Level = DifficultyLevel.Beginner,
            IsDeleted = false,
        });
    }

    private Module SeedModule(Guid? id = null, ModuleType moduleType = ModuleType.Theory)
    {
        SeedProgram();
        var module = new Module
        {
            Id = id ?? _moduleId,
            Code = id == _experientialModuleId ? "MOD-EXP" : "MOD-001",
            Name = "Module",
            ProgramId = _programId,
            ModuleType = moduleType,
            ModuleOrder = id == _experientialModuleId ? 2 : 1,
            IsDeleted = false,
        };
        _db.Modules.Seed(module);
        return module;
    }

    private Course SeedCourse(Guid? id = null, Guid? moduleId = null)
    {
        SeedModule(moduleId ?? _moduleId);
        var course = new Course
        {
            Id = id ?? _courseId,
            Code = id == _otherCourseId ? "CRS-002" : "CRS-001",
            Name = "Course",
            ModuleId = moduleId ?? _moduleId,
            IsDeleted = false,
        };
        _db.Courses.Seed(course);
        return course;
    }

    private Activity SeedActivity(
        Guid? id = null,
        Guid? courseId = null,
        string code = "ACT-001",
        ActivityType activityType = ActivityType.SelfPaced,
        int activityOrder = 1,
        bool isDeleted = false)
    {
        SeedCourse(courseId ?? _courseId);
        var activity = new Activity
        {
            Id = id ?? _activityId,
            Code = code,
            Name = "Activity",
            CourseId = courseId ?? _courseId,
            ActivityType = activityType,
            ActivityOrder = activityOrder,
            IsDeleted = isDeleted,
        };
        _db.Activities.Seed(activity);
        return activity;
    }

    private static CreateActivitiesRequestDto SelfPacedCreate(
        Guid courseId,
        int order = 1,
        string code = "ACT-NEW") =>
        new()
        {
            Code = code,
            CourseId = courseId,
            Name = "Self-paced lesson",
            ActivityType = ActivityType.SelfPaced,
            ActivityOrder = order,
        };

    // ── GetAllActivitiesAsync ─────────────────────────────────────────────────

    [Fact]
    public async Task GetAll_ReturnsFilteredSortedPage()
    {
        SeedActivity();
        SeedActivity(
            id: _activity2Id,
            code: "ACT-002",
            activityType: ActivityType.LiveOnline,
            activityOrder: 2);
        var sut = CreateSut();

        var result = await sut.GetAllActivitiesAsync(
            "act", "name", false, 1, 10, code: "001", activityType: ActivityType.SelfPaced);

        Assert.Equal(1, result.TotalCount);
        Assert.Equal("ACT-001", result.Items[0].Code);
    }

    [Fact]
    public async Task GetAll_AppliesAlternateSortColumns()
    {
        var (start, end) = FutureSchedule();
        var first = SeedActivity(code: "ACT-A", activityOrder: 1);
        first.CreatedAt = DateTime.UtcNow.AddDays(-5);
        first.StartTime = start;
        first.EndTime = end;

        var second = SeedActivity(
            id: _activity2Id,
            code: "ACT-B",
            activityType: ActivityType.LiveOnline,
            activityOrder: 2);
        second.CreatedAt = DateTime.UtcNow.AddDays(-1);
        second.StartTime = start.AddDays(1);
        second.EndTime = end.AddDays(1);
        second.Location = "Room A";

        var sut = CreateSut();

        var byOrder = await sut.GetAllActivitiesAsync(null, "activityorder", true, 1, 10, null, null);
        var byType = await sut.GetAllActivitiesAsync(null, "activitytype", false, 1, 10, null, null);
        var byStart = await sut.GetAllActivitiesAsync(null, "starttime", true, 1, 10, null, null);

        Assert.Equal("ACT-B", byOrder.Items[0].Code);
        Assert.Equal(ActivityType.SelfPaced, byType.Items[0].ActivityType);
        Assert.Equal("ACT-B", byStart.Items[0].Code);
    }

    // ── GetActivityByIdAsync ──────────────────────────────────────────────────

    [Fact]
    public async Task GetById_ReturnsActivityWithMaterial()
    {
        SeedActivity();
        _db.Materials.Seed(new Material
        {
            Id = _materialId,
            ActivityId = _activityId,
            Title = "Slides",
            MaterialType = MaterialType.PDF,
            FileUrl = "https://cdn.example.com/slides.pdf",
            IsDeleted = false,
        });
        var sut = CreateSut();

        var result = await sut.GetActivityByIdAsync(_activityId);

        Assert.NotNull(result);
        Assert.NotNull(result!.Material);
        Assert.Equal("Slides", result.Material!.Title);
    }

    [Fact]
    public async Task GetById_ReturnsNull_WhenMissingOrDeleted()
    {
        SeedActivity(isDeleted: true);
        var sut = CreateSut();

        Assert.Null(await sut.GetActivityByIdAsync(_activityId));
        Assert.Null(await sut.GetActivityByIdAsync(Guid.NewGuid()));
    }

    // ── CreateActivityAsync ───────────────────────────────────────────────────

    [Fact]
    public async Task Create_PersistsSelfPacedActivity()
    {
        SeedCourse();
        var sut = CreateSut();

        var result = await sut.CreateActivityAsync(SelfPacedCreate(_courseId));

        Assert.Equal("ACT-NEW", result.Code);
        Assert.Equal(ActivityType.SelfPaced, result.ActivityType);
        Assert.Equal(1, _db.SaveChangesCallCount);
    }

    [Fact]
    public async Task Create_ShiftsExistingActivities_WhenInsertingInMiddle()
    {
        SeedActivity(activityOrder: 1);
        SeedActivity(id: _activity2Id, code: "ACT-002", activityOrder: 2);
        var sut = CreateSut();

        await sut.CreateActivityAsync(SelfPacedCreate(_courseId, order: 1, code: "ACT-NEW"));

        Assert.Equal(2, _db.Activities.Items.Single(a => a.Code == "ACT-001").ActivityOrder);
        Assert.Equal(1, _db.Activities.Items.Single(a => a.Code == "ACT-NEW").ActivityOrder);
    }

    [Fact]
    public async Task Create_Throws_WhenCourseMissingOrCodeDuplicate()
    {
        var sut = CreateSut();

        await Assert.ThrowsAsync<NotFoundException>(() =>
            sut.CreateActivityAsync(SelfPacedCreate(_courseId)));
        SeedActivity();
        await Assert.ThrowsAsync<ConflictException>(() =>
            sut.CreateActivityAsync(SelfPacedCreate(_courseId, code: "ACT-001")));
    }

    [Fact]
    public async Task Create_Throws_WhenActivityTypeNotAllowedForTheoryModule()
    {
        SeedCourse();
        var sut = CreateSut();

        await Assert.ThrowsAsync<BadRequestException>(() =>
            sut.CreateActivityAsync(new CreateActivitiesRequestDto
            {
                Code = "ACT-OFF",
                CourseId = _courseId,
                Name = "Field trip",
                ActivityType = ActivityType.Offline,
                ActivityOrder = 1,
                Location = "Campus",
                StartTime = FutureSchedule().Start,
                EndTime = FutureSchedule().End,
            }));
    }

    [Fact]
    public async Task Create_Throws_WhenLiveOnlineMissingSchedule()
    {
        SeedCourse();
        var sut = CreateSut();

        await Assert.ThrowsAsync<BadRequestException>(() =>
            sut.CreateActivityAsync(new CreateActivitiesRequestDto
            {
                Code = "ACT-LIVE",
                CourseId = _courseId,
                Name = "Live session",
                ActivityType = ActivityType.LiveOnline,
                ActivityOrder = 1,
            }));
    }

    [Fact]
    public async Task Create_AllowsOffline_OnExperientialModule()
    {
        SeedProgram();
        SeedModule(_experientialModuleId, ModuleType.Experiential);
        _db.Courses.Seed(new Course
        {
            Id = _otherCourseId,
            Code = "CRS-002",
            Name = "Workshop Course",
            ModuleId = _experientialModuleId,
            IsDeleted = false,
        });
        var (start, end) = FutureSchedule();
        var sut = CreateSut();

        var result = await sut.CreateActivityAsync(new CreateActivitiesRequestDto
        {
            Code = "ACT-OFF",
            CourseId = _otherCourseId,
            Name = "Workshop",
            ActivityType = ActivityType.Offline,
            ActivityOrder = 1,
            Location = "Lab",
            StartTime = start,
            EndTime = end,
            RequireQrCheckin = true,
        });

        Assert.Equal(ActivityType.Offline, result.ActivityType);
        Assert.True(result.RequireQrCheckin);
    }

    [Fact]
    public async Task Create_Throws_WhenActivityOrderOutOfRange()
    {
        SeedCourse();
        var sut = CreateSut();

        await Assert.ThrowsAsync<BadRequestException>(() =>
            sut.CreateActivityAsync(SelfPacedCreate(_courseId, order: 3)));
    }

    // ── UpdateActivityAsync ───────────────────────────────────────────────────

    [Fact]
    public async Task Update_AppliesChanges()
    {
        SeedActivity();
        var sut = CreateSut();

        var result = await sut.UpdateActivityAsync(_activityId, new UpdateActivitiesRequestDto
        {
            Name = "Updated activity",
            Description = "Updated desc",
        });

        Assert.NotNull(result);
        Assert.Equal("Updated activity", result!.Name);
    }

    [Fact]
    public async Task Update_ReordersWithinCourse()
    {
        SeedActivity(activityOrder: 1);
        SeedActivity(id: _activity2Id, code: "ACT-002", activityOrder: 2);
        var sut = CreateSut();

        await sut.UpdateActivityAsync(_activityId, new UpdateActivitiesRequestDto { ActivityOrder = 2 });

        Assert.Equal(2, _db.Activities.Items.Single(a => a.Id == _activityId).ActivityOrder);
        Assert.Equal(1, _db.Activities.Items.Single(a => a.Id == _activity2Id).ActivityOrder);
    }

    [Fact]
    public async Task Update_MovesActivityToAnotherCourse()
    {
        SeedActivity(activityOrder: 1);
        SeedCourse(_otherCourseId, _experientialModuleId);
        SeedActivity(id: _activity3Id, courseId: _otherCourseId, code: "ACT-003", activityOrder: 1);
        var sut = CreateSut();

        await sut.UpdateActivityAsync(_activityId, new UpdateActivitiesRequestDto
        {
            CourseId = _otherCourseId,
            ActivityOrder = 1,
        });

        Assert.Equal(_otherCourseId, _db.Activities.Items.Single(a => a.Id == _activityId).CourseId);
        Assert.Equal(1, _db.Activities.Items.Single(a => a.Id == _activityId).ActivityOrder);
    }

    [Fact]
    public async Task Update_ClearsSchedule_WhenSwitchingToSelfPaced()
    {
        var (start, end) = FutureSchedule();
        var activity = SeedActivity(activityType: ActivityType.LiveOnline);
        activity.Location = "Zoom";
        activity.StartTime = start;
        activity.EndTime = end;
        activity.MaxCapacity = 30;
        activity.RequireQrCheckin = false;
        var sut = CreateSut();

        var result = await sut.UpdateActivityAsync(_activityId, new UpdateActivitiesRequestDto
        {
            ActivityType = ActivityType.SelfPaced,
        });

        Assert.Equal(ActivityType.SelfPaced, result!.ActivityType);
        Assert.Null(result.Location);
        Assert.Null(result.StartTime);
        Assert.Null(result.EndTime);
        Assert.Null(result.MaxCapacity);
        Assert.False(result.RequireQrCheckin);
    }

    [Fact]
    public async Task Update_ReturnsNull_WhenMissing()
    {
        var sut = CreateSut();

        Assert.Null(await sut.UpdateActivityAsync(_activityId, new UpdateActivitiesRequestDto { Name = "X" }));
    }

    [Fact]
    public async Task Update_Throws_WhenCodeDuplicate()
    {
        SeedActivity();
        SeedActivity(id: _activity2Id, code: "ACT-002", activityOrder: 2);
        var sut = CreateSut();

        await Assert.ThrowsAsync<ConflictException>(() =>
            sut.UpdateActivityAsync(_activityId, new UpdateActivitiesRequestDto { Code = "ACT-002" }));
    }

    // ── DeleteActivityAsync ───────────────────────────────────────────────────

    [Fact]
    public async Task Delete_SoftDeletesActivity()
    {
        SeedActivity();
        var sut = CreateSut();

        var deleted = await sut.DeleteActivityAsync(_activityId);

        Assert.True(deleted);
        Assert.True(_db.Activities.Items[0].IsDeleted);
    }

    [Fact]
    public async Task Delete_ReturnsFalse_WhenMissing()
    {
        var sut = CreateSut();

        Assert.False(await sut.DeleteActivityAsync(_activityId));
    }
}
