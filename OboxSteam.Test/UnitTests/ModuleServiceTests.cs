using Microsoft.Extensions.Logging.Abstractions;
using OboxSteam.Application.DTOs.ModuleDTO;
using OboxSteam.Application.Exceptions;
using OboxSteam.Application.Services;
using OboxSteam.Domain.Entities;
using OboxSteam.Domain.Enums;
using OboxSteam.Test.Helpers;

namespace OboxSteam.Test.UnitTests;

public sealed class ModuleServiceTests
{
    private readonly Guid _programId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private readonly Guid _otherProgramId = Guid.Parse("23232323-2323-2323-2323-232323232323");
    private readonly Guid _moduleId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private readonly Guid _prerequisiteId = Guid.Parse("34343434-3434-3434-3434-343434343434");
    private readonly Guid _otherModuleId = Guid.Parse("35353535-3535-3535-3535-353535353535");
    private readonly Guid _courseId = Guid.Parse("44444444-4444-4444-4444-444444444444");

    private readonly InMemoryUnitOfWork _db = new();

    private ModuleService CreateSut() =>
        new(_db, NullLogger<ModuleService>.Instance, new FakeSyncEventPublisher());

    private void SeedProgram(Guid? id = null)
    {
        _db.Programs.Seed(new Program
        {
            Id = id ?? _programId,
            Code = id == _otherProgramId ? "PRG-002" : "PRG-001",
            Name = "Program",
            Category = ProgramCategory.Technology,
            Level = DifficultyLevel.Beginner,
            IsDeleted = false,
        });
    }

    private Module SeedModule(
        Guid? id = null,
        string code = "MOD-001",
        string name = "Intro Module",
        Guid? programId = null,
        int moduleOrder = 1,
        Guid? prerequisiteModuleId = null,
        List<Course>? courses = null,
        bool isDeleted = false)
    {
        SeedProgram(programId ?? _programId);
        var module = new Module
        {
            Id = id ?? _moduleId,
            Code = code,
            Name = name,
            ProgramId = programId ?? _programId,
            ModuleType = ModuleType.Theory,
            ModuleOrder = moduleOrder,
            PrerequisiteModuleId = prerequisiteModuleId,
            LearningOutcomes = ["Outcome 1"],
            Courses = courses ?? [],
            IsDeleted = isDeleted,
        };
        _db.Modules.Seed(module);
        if (courses is { Count: > 0 })
            _db.Courses.Seed(courses.ToArray());
        return module;
    }

    private static CreateModuleRequestDto ValidCreateRequest(
        Guid programId,
        int moduleOrder = 1,
        Guid? prerequisiteModuleId = null) =>
        new()
        {
            Code = "MOD-NEW",
            ProgramId = programId,
            Name = "New Module",
            ModuleType = ModuleType.Theory,
            ModuleOrder = moduleOrder,
            PrerequisiteModuleId = prerequisiteModuleId,
            Price = 10m,
            RetakeFee = 5m,
        };

    // ── GetModuleByIdAsync ────────────────────────────────────────────────────

    [Fact]
    public async Task GetById_ReturnsModuleWithCourses()
    {
        var course = new Course
        {
            Id = _courseId,
            Code = "CRS-001",
            Name = "Intro Course",
            ModuleId = _moduleId,
            IsDeleted = false,
        };
        SeedModule(courses: [course]);
        var sut = CreateSut();

        var result = await sut.GetModuleByIdAsync(_moduleId);

        Assert.Equal("Intro Module", result.Name);
        Assert.Single(result.Courses);
        Assert.Equal("Intro Course", result.Courses[0].Name);
    }

    [Fact]
    public async Task GetById_Throws_WhenMissingOrDeleted()
    {
        SeedModule(isDeleted: true);
        var sut = CreateSut();

        await Assert.ThrowsAsync<NotFoundException>(() => sut.GetModuleByIdAsync(_moduleId));
        await Assert.ThrowsAsync<NotFoundException>(() => sut.GetModuleByIdAsync(Guid.NewGuid()));
    }

    // ── GetModuleByNameAsync ──────────────────────────────────────────────────

    [Fact]
    public async Task GetByName_ReturnsModule()
    {
        SeedModule();
        var sut = CreateSut();

        var result = await sut.GetModuleByNameAsync("intro module");

        Assert.Equal(_moduleId, result.Id);
    }

    [Fact]
    public async Task GetByName_Throws_WhenMissing()
    {
        var sut = CreateSut();

        await Assert.ThrowsAsync<NotFoundException>(() => sut.GetModuleByNameAsync("Missing"));
    }

    // ── GetAllModulesAsync ────────────────────────────────────────────────────

    [Fact]
    public async Task GetAll_ReturnsFilteredSortedPageWithCourses()
    {
        SeedModule();
        SeedModule(
            id: _otherModuleId,
            code: "MOD-002",
            name: "Advanced Module",
            moduleOrder: 2);
        _db.Courses.Seed(new Course
        {
            Id = _courseId,
            Code = "CRS-001",
            Name = "Course",
            ModuleId = _moduleId,
            IsDeleted = false,
        });
        var sut = CreateSut();

        var result = await sut.GetAllModulesAsync(
            "intro", "name", false, 1, 10, code: "mod", moduleType: ModuleType.Theory);

        Assert.Equal(1, result.TotalCount);
        Assert.Single(result.Items[0].Courses);
    }

    [Fact]
    public async Task GetAll_AppliesAlternateSortColumns()
    {
        var older = SeedModule(name: "Alpha", code: "MOD-A", moduleOrder: 1);
        older.CreatedAt = DateTime.UtcNow.AddDays(-10);
        var newer = SeedModule(
            id: _otherModuleId,
            name: "Beta",
            code: "MOD-B",
            moduleOrder: 2);
        newer.CreatedAt = DateTime.UtcNow.AddDays(-1);
        var sut = CreateSut();

        var byOrder = await sut.GetAllModulesAsync(null, "moduleorder", true, 1, 10, null, null);
        var byType = await sut.GetAllModulesAsync(null, "moduletype", false, 1, 10, null, null);
        var byCode = await sut.GetAllModulesAsync(null, "code", true, 1, 10, null, null);

        Assert.Equal("Beta", byOrder.Items[0].Name);
        Assert.Equal("Alpha", byType.Items[0].Name);
        Assert.Equal("Beta", byCode.Items[0].Name);
    }

    [Theory]
    [InlineData("name", false)]
    [InlineData("code", true)]
    [InlineData("createdat", false)]
    [InlineData("xxx", true)]
    public async Task GetAll_SortByExtraColumns_ReturnsResults(string sortBy, bool desc)
    {
        SeedModule();
        var sut = CreateSut();

        var result = await sut.GetAllModulesAsync(null, sortBy, desc, 1, 10, null, null);

        Assert.True(result.TotalCount >= 1);
    }

    // ── CreateModuleAsync ─────────────────────────────────────────────────────

    [Fact]
    public async Task Create_PersistsModule()
    {
        SeedProgram();
        var sut = CreateSut();

        var result = await sut.CreateModuleAsync(ValidCreateRequest(_programId));

        Assert.Equal("MOD-NEW", result.Code);
        Assert.Equal(1, result.ModuleOrder);
        Assert.Single(_db.Modules.Items);
    }

    [Fact]
    public async Task Create_Throws_WhenProgramMissingOrCodeDuplicate()
    {
        SeedModule();
        var sut = CreateSut();

        await Assert.ThrowsAsync<NotFoundException>(() =>
            sut.CreateModuleAsync(ValidCreateRequest(Guid.NewGuid(), moduleOrder: 2)));
        await Assert.ThrowsAsync<ConflictException>(() =>
            sut.CreateModuleAsync(new CreateModuleRequestDto
            {
                Code = "mod-001",
                ProgramId = _programId,
                Name = "Dup",
                ModuleType = ModuleType.Theory,
                ModuleOrder = 2,
            }));
    }

    [Fact]
    public async Task Create_Throws_WhenPrerequisiteInvalid()
    {
        SeedProgram();
        SeedModule(id: _prerequisiteId, code: "MOD-PRE", name: "Prereq", programId: _otherProgramId);
        var sut = CreateSut();

        await Assert.ThrowsAsync<NotFoundException>(() =>
            sut.CreateModuleAsync(ValidCreateRequest(_programId, moduleOrder: 1, prerequisiteModuleId: Guid.NewGuid())));
        await Assert.ThrowsAsync<BadRequestException>(() =>
            sut.CreateModuleAsync(ValidCreateRequest(_programId, moduleOrder: 1, prerequisiteModuleId: _prerequisiteId)));
    }

    [Fact]
    public async Task Create_Throws_WhenModuleOrderNotGreaterThanMax()
    {
        SeedModule(moduleOrder: 1);
        var sut = CreateSut();

        await Assert.ThrowsAsync<BadRequestException>(() =>
            sut.CreateModuleAsync(ValidCreateRequest(_programId, moduleOrder: 1)));
    }

    // ── UpdateModuleAsync ─────────────────────────────────────────────────────

    [Fact]
    public async Task Update_AppliesChanges()
    {
        SeedModule();
        var sut = CreateSut();

        var result = await sut.UpdateModuleAsync(_moduleId, new UpdateModuleRequestDto
        {
            Name = "Updated Module",
        });

        Assert.Equal("Updated Module", result.Name);
        Assert.Equal("Updated Module", _db.Modules.Items[0].Name);
    }

    [Fact]
    public async Task Update_ReturnsUnchanged_WhenNoChanges()
    {
        SeedModule();
        var sut = CreateSut();

        var result = await sut.UpdateModuleAsync(_moduleId, new UpdateModuleRequestDto());

        Assert.Equal("Intro Module", result.Name);
        Assert.Equal(0, _db.SaveChangesCallCount);
    }

    [Fact]
    public async Task Update_Throws_WhenMissingDuplicateCodeOrBadPrerequisite()
    {
        SeedModule();
        SeedModule(id: _otherModuleId, code: "MOD-002", name: "Other", moduleOrder: 2);
        var sut = CreateSut();

        await Assert.ThrowsAsync<NotFoundException>(() =>
            sut.UpdateModuleAsync(Guid.NewGuid(), new UpdateModuleRequestDto { Name = "X" }));
        await Assert.ThrowsAsync<ConflictException>(() =>
            sut.UpdateModuleAsync(_moduleId, new UpdateModuleRequestDto { Code = "MOD-002" }));
        await Assert.ThrowsAsync<BadRequestException>(() =>
            sut.UpdateModuleAsync(_moduleId, new UpdateModuleRequestDto { PrerequisiteModuleId = _moduleId }));
    }

    [Fact]
    public async Task Update_ChangesProgramAndPrerequisite()
    {
        SeedModule();
        SeedModule(id: _prerequisiteId, code: "MOD-PRE", name: "Prerequisite", moduleOrder: 2);
        var sut = CreateSut();

        var result = await sut.UpdateModuleAsync(_moduleId, new UpdateModuleRequestDto
        {
            ProgramId = _programId,
            PrerequisiteModuleId = _prerequisiteId,
            LearningOutcomes = ["Outcome A", "Outcome B"],
        });

        Assert.Equal(_prerequisiteId, result.PrerequisiteModuleId);
        Assert.Equal(2, result.LearningOutcomes.Length);
    }

    // ── DeleteModuleAsync ─────────────────────────────────────────────────────

    [Fact]
    public async Task Delete_SoftDeletesModule()
    {
        SeedModule();
        var sut = CreateSut();

        var deleted = await sut.DeleteModuleAsync(_moduleId);

        Assert.True(deleted);
        Assert.True(_db.Modules.Items[0].IsDeleted);
    }

    [Fact]
    public async Task Delete_Throws_WhenMissing()
    {
        var sut = CreateSut();

        await Assert.ThrowsAsync<NotFoundException>(() => sut.DeleteModuleAsync(_moduleId));
    }
}
