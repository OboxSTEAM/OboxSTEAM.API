using Microsoft.Extensions.Logging.Abstractions;
using OboxSteam.Application.DTOs.CourseDTO;
using OboxSteam.Application.Exceptions;
using OboxSteam.Application.Services;
using OboxSteam.Domain.Entities;
using OboxSteam.Domain.Enums;
using OboxSteam.Test.Helpers;

namespace OboxSteam.Test.UnitTests;

public sealed class CourseServiceTests
{
    private readonly Guid _programId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private readonly Guid _moduleId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private readonly Guid _otherModuleId = Guid.Parse("34343434-3434-3434-3434-343434343434");
    private readonly Guid _courseId = Guid.Parse("44444444-4444-4444-4444-444444444444");
    private readonly Guid _otherCourseId = Guid.Parse("45454545-4545-4545-4545-454545454545");
    private readonly Guid _activityId = Guid.Parse("55555555-5555-5555-5555-555555555555");

    private readonly InMemoryUnitOfWork _db = new();

    private CourseService CreateSut() =>
        new(_db, NullLogger<CourseService>.Instance);

    private Module SeedModule(Guid? id = null, string name = "Intro Module")
    {
        if (_db.Programs.Items.Count == 0)
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

        var module = new Module
        {
            Id = id ?? _moduleId,
            Code = id == _otherModuleId ? "MOD-002" : "MOD-001",
            Name = name,
            ProgramId = _programId,
            ModuleType = ModuleType.Theory,
            ModuleOrder = id == _otherModuleId ? 2 : 1,
            IsDeleted = false,
        };
        _db.Modules.Seed(module);
        return module;
    }

    private Course SeedCourse(
        Guid? id = null,
        string name = "Intro Course",
        string code = "CRS-001",
        Guid? moduleId = null,
        List<Activity>? activities = null,
        bool isDeleted = false)
    {
        var module = SeedModule(moduleId ?? _moduleId);
        var course = new Course
        {
            Id = id ?? _courseId,
            Code = code,
            Name = name,
            ModuleId = module.Id,
            Module = module,
            Description = "Course desc",
            Activities = activities ?? [],
            CreatedAt = DateTime.UtcNow.AddDays(-3),
            IsDeleted = isDeleted,
        };
        _db.Courses.Seed(course);
        if (activities is { Count: > 0 })
            _db.Activities.Seed(activities.ToArray());
        return course;
    }

    // ── GetAllCoursesAsync ────────────────────────────────────────────────────

    [Fact]
    public async Task GetAll_ReturnsFilteredSortedPage()
    {
        SeedCourse();
        SeedCourse(
            id: _otherCourseId,
            name: "Advanced Course",
            code: "CRS-002",
            moduleId: _otherModuleId);
        var sut = CreateSut();

        var result = await sut.GetAllCoursesAsync(
            "intro", "name", false, 1, 10, code: "crs", moduleName: "intro");

        Assert.Equal(1, result.TotalCount);
        Assert.Equal("Intro Course", result.Items[0].Name);
    }

    [Fact]
    public async Task GetAll_AppliesAlternateSortColumns()
    {
        var older = SeedCourse(name: "Alpha", code: "CRS-A");
        older.CreatedAt = DateTime.UtcNow.AddDays(-10);
        var newer = SeedCourse(
            id: _otherCourseId,
            name: "Beta",
            code: "CRS-B",
            moduleId: _otherModuleId);
        newer.CreatedAt = DateTime.UtcNow.AddDays(-1);
        var sut = CreateSut();

        var byName = await sut.GetAllCoursesAsync(null, "name", false, 1, 10, null, null);
        var byCode = await sut.GetAllCoursesAsync(null, "code", true, 1, 10, null, null);
        var byModuleId = await sut.GetAllCoursesAsync(null, "moduleid", false, 1, 10, null, null);
        var byCreatedAt = await sut.GetAllCoursesAsync(null, "createdat", true, 1, 10, null, null);
        var byDefault = await sut.GetAllCoursesAsync(null, "xxx", false, 1, 10, null, null);

        Assert.Equal("CRS-B", byCode.Items[0].Code);
        Assert.Equal(_moduleId, byModuleId.Items[0].ModuleId);
        Assert.Equal("Beta", byCreatedAt.Items[0].Name);
    }

    // ── GetCourseByIdAsync ────────────────────────────────────────────────────

    [Fact]
    public async Task GetById_ReturnsCourseWithActivities()
    {
        var activity = new Activity
        {
            Id = _activityId,
            Code = "ACT-001",
            Name = "Lesson 1",
            CourseId = _courseId,
            ActivityType = ActivityType.SelfPaced,
            ActivityOrder = 1,
            IsDeleted = false,
        };
        SeedCourse(activities: [activity]);
        var sut = CreateSut();

        var result = await sut.GetCourseByIdAsync(_courseId);

        Assert.NotNull(result);
        Assert.Single(result!.Activities);
        Assert.Equal("Lesson 1", result.Activities[0].Name);
    }

    [Fact]
    public async Task GetById_ReturnsNull_WhenMissingOrDeleted()
    {
        SeedCourse(isDeleted: true);
        var sut = CreateSut();

        Assert.Null(await sut.GetCourseByIdAsync(_courseId));
        Assert.Null(await sut.GetCourseByIdAsync(Guid.NewGuid()));
    }

    // ── GetCourseByNameAsync ──────────────────────────────────────────────────

    [Fact]
    public async Task GetByName_ReturnsCourse()
    {
        SeedCourse();
        var sut = CreateSut();

        var result = await sut.GetCourseByNameAsync("intro course");

        Assert.NotNull(result);
        Assert.Equal(_courseId, result!.Id);
    }

    [Fact]
    public async Task GetByName_Throws_WhenNameMissing()
    {
        var sut = CreateSut();

        await Assert.ThrowsAsync<BadRequestException>(() => sut.GetCourseByNameAsync("  "));
    }

    [Fact]
    public async Task GetByName_ReturnsNull_WhenNotFound()
    {
        var sut = CreateSut();

        Assert.Null(await sut.GetCourseByNameAsync("Missing"));
    }

    // ── CreateCourseAsync ───────────────────────────────────────────────────────

    [Fact]
    public async Task Create_PersistsCourse()
    {
        SeedModule();
        var sut = CreateSut();

        var result = await sut.CreateCourseAsync(new CreateCourseRequestDto
        {
            Code = "CRS-NEW",
            ModuleId = _moduleId,
            Name = "New Course",
            Description = "Desc",
        });

        Assert.Equal("CRS-NEW", result.Code);
        Assert.Single(_db.Courses.Items);
        Assert.Equal(1, _db.SaveChangesCallCount);
    }

    [Fact]
    public async Task Create_Throws_WhenModuleMissing()
    {
        var sut = CreateSut();

        await Assert.ThrowsAsync<NotFoundException>(() =>
            sut.CreateCourseAsync(new CreateCourseRequestDto
            {
                Code = "CRS-001",
                ModuleId = _moduleId,
                Name = "Course",
            }));
    }

    [Fact]
    public async Task Create_Throws_WhenCodeDuplicate()
    {
        SeedCourse();
        var sut = CreateSut();

        await Assert.ThrowsAsync<ConflictException>(() =>
            sut.CreateCourseAsync(new CreateCourseRequestDto
            {
                Code = "crs-001",
                ModuleId = _moduleId,
                Name = "Duplicate",
            }));
    }

    // ── UpdateCourseAsync ─────────────────────────────────────────────────────

    [Fact]
    public async Task Update_AppliesChanges()
    {
        SeedCourse();
        var sut = CreateSut();

        var result = await sut.UpdateCourseAsync(_courseId, new UpdateCourseRequestDto
        {
            Name = "Updated Course",
            Description = "Updated desc",
        });

        Assert.NotNull(result);
        Assert.Equal("Updated Course", result!.Name);
        Assert.Equal("Updated desc", _db.Courses.Items[0].Description);
    }

    [Fact]
    public async Task Update_MovesCourseToAnotherModule()
    {
        SeedCourse();
        SeedModule(_otherModuleId, "Advanced Module");
        var sut = CreateSut();

        var result = await sut.UpdateCourseAsync(_courseId, new UpdateCourseRequestDto
        {
            ModuleId = _otherModuleId,
        });

        Assert.Equal(_otherModuleId, result!.ModuleId);
    }

    [Fact]
    public async Task Update_ReturnsNull_WhenMissing()
    {
        var sut = CreateSut();

        Assert.Null(await sut.UpdateCourseAsync(_courseId, new UpdateCourseRequestDto { Name = "X" }));
    }

    [Fact]
    public async Task Update_Throws_WhenCodeOrModuleInvalid()
    {
        SeedCourse();
        SeedCourse(id: _otherCourseId, name: "Other", code: "CRS-002", moduleId: _otherModuleId);
        var sut = CreateSut();

        await Assert.ThrowsAsync<ConflictException>(() =>
            sut.UpdateCourseAsync(_courseId, new UpdateCourseRequestDto { Code = "CRS-002" }));
        await Assert.ThrowsAsync<NotFoundException>(() =>
            sut.UpdateCourseAsync(_courseId, new UpdateCourseRequestDto { ModuleId = Guid.NewGuid() }));
    }

    // ── DeleteCourseAsync ─────────────────────────────────────────────────────

    [Fact]
    public async Task Delete_SoftDeletesCourse()
    {
        SeedCourse();
        var sut = CreateSut();

        var deleted = await sut.DeleteCourseAsync(_courseId);

        Assert.True(deleted);
        Assert.True(_db.Courses.Items[0].IsDeleted);
    }

    [Fact]
    public async Task Delete_ReturnsFalse_WhenMissing()
    {
        var sut = CreateSut();

        Assert.False(await sut.DeleteCourseAsync(_courseId));
    }
}
