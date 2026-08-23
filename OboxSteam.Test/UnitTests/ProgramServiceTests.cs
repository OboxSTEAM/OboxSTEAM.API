using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OboxSteam.Application.DTOs.ProgramDTO;
using OboxSteam.Application.Exceptions;
using OboxSteam.Application.Interfaces;
using OboxSteam.Application.Services;
using OboxSteam.Domain.Entities;
using OboxSteam.Domain.Enums;
using OboxSteam.Test.Helpers;

namespace OboxSteam.Test.UnitTests;

public sealed class ProgramServiceTests
{
    private readonly Guid _programId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private readonly Guid _otherProgramId = Guid.Parse("23232323-2323-2323-2323-232323232323");
    private readonly Guid _moduleId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private readonly Guid _courseId = Guid.Parse("44444444-4444-4444-4444-444444444444");
    private readonly Guid _activityId = Guid.Parse("55555555-5555-5555-5555-555555555555");
    private readonly Guid _expertId = Guid.Parse("66666666-6666-6666-6666-666666666666");

    private readonly InMemoryUnitOfWork _db = new();
    private readonly Mock<IBlobService> _blobService = new();

    private ProgramService CreateSut() =>
        new(_db, _blobService.Object, NullLogger<ProgramService>.Instance);

    private static IFormFile CreateThumbnailFile(
        string fileName = "thumb.jpg",
        string contentType = "image/jpeg",
        int size = 1024)
    {
        var content = new byte[size];
        var stream = new MemoryStream(content);
        var file = new Mock<IFormFile>();
        file.Setup(f => f.FileName).Returns(fileName);
        file.Setup(f => f.ContentType).Returns(contentType);
        file.Setup(f => f.Length).Returns(size);
        file.Setup(f => f.OpenReadStream()).Returns(stream);
        return file.Object;
    }

    private Module SeedModule(Guid? programId = null, int moduleOrder = 1)
    {
        var module = new Module
        {
            Id = _moduleId,
            Code = "MOD-001",
            Name = "Intro Module",
            ProgramId = programId ?? _programId,
            ModuleType = ModuleType.Theory,
            ModuleOrder = moduleOrder,
            IsDeleted = false,
        };
        _db.Modules.Seed(module);
        return module;
    }

    private Program SeedProgram(
        Guid? id = null,
        string name = "STEAM Program",
        string code = "PRG-001",
        List<Module>? modules = null,
        bool isDeleted = false)
    {
        var program = new Program
        {
            Id = id ?? _programId,
            Code = code,
            Name = name,
            SeriesName = "Series A",
            Description = "Desc",
            Category = ProgramCategory.Technology,
            Level = DifficultyLevel.Beginner,
            SkillsGained = "Robotics, Coding",
            Rating = 4.5m,
            Status = ProgramStatus.Active,
            Price = 100m,
            Modules = modules ?? [],
            CreatedAt = DateTime.UtcNow.AddDays(-5),
            IsDeleted = isDeleted,
        };
        _db.Programs.Seed(program);
        return program;
    }

    private void SeedExpertOnProgram(Guid programId = default)
    {
        _db.Experts.Seed(new Expert
        {
            Id = _expertId,
            Code = "EXP-001",
            FullName = "Dr. Ada",
            Title = "Lead",
            Organization = "Obox",
            IsDeleted = false,
        });
        _db.ProgramBoards.Seed(new ProgramBoard
        {
            Id = Guid.Parse("77777777-7777-7777-7777-777777777777"),
            ProgramId = programId == default ? _programId : programId,
            ExpertId = _expertId,
            RoleInBoard = "Advisor",
            IsDeleted = false,
        });
    }

    // ── GetProgramByIdAsync ───────────────────────────────────────────────────

    [Fact]
    public async Task GetById_ReturnsProgramWithModulesAndExperts()
    {
        var module = SeedModule();
        SeedProgram(modules: [module]);
        SeedExpertOnProgram();
        var sut = CreateSut();

        var result = await sut.GetProgramByIdAsync(_programId);

        Assert.Equal("STEAM Program", result.Name);
        Assert.Single(result.Modules);
        Assert.Equal("Intro Module", result.Modules[0].Name);
        Assert.Single(result.Experts);
        Assert.Equal("Dr. Ada", result.Experts[0].FullName);
    }

    [Fact]
    public async Task GetById_Throws_WhenMissingOrDeleted()
    {
        SeedProgram(isDeleted: true);
        var sut = CreateSut();

        await Assert.ThrowsAsync<NotFoundException>(() => sut.GetProgramByIdAsync(_programId));
        await Assert.ThrowsAsync<NotFoundException>(() => sut.GetProgramByIdAsync(Guid.NewGuid()));
    }

    // ── GetProgramByNameAsync ─────────────────────────────────────────────────

    [Fact]
    public async Task GetByName_ReturnsProgram()
    {
        SeedProgram();
        var sut = CreateSut();

        var result = await sut.GetProgramByNameAsync("steam program");

        Assert.Equal(_programId, result.Id);
    }

    [Fact]
    public async Task GetByName_Throws_WhenMissing()
    {
        var sut = CreateSut();

        await Assert.ThrowsAsync<NotFoundException>(() => sut.GetProgramByNameAsync("Missing"));
    }

    // ── GetProgramCurriculumAsync ─────────────────────────────────────────────

    [Fact]
    public async Task GetCurriculum_ReturnsCompactTree()
    {
        var module = SeedModule();
        SeedProgram(modules: [module]);
        _db.Courses.Seed(new Course
        {
            Id = _courseId,
            Code = "CRS-001",
            Name = "Intro Course",
            ModuleId = _moduleId,
            IsDeleted = false,
        });
        _db.Activities.Seed(new Activity
        {
            Id = _activityId,
            Code = "ACT-001",
            Name = "Lesson 1",
            CourseId = _courseId,
            ActivityType = ActivityType.SelfPaced,
            ActivityOrder = 1,
            IsDeleted = false,
        });
        var sut = CreateSut();

        var result = await sut.GetProgramCurriculumAsync(_programId);

        Assert.Equal(_programId, result.ProgramId);
        Assert.Single(result.Modules);
        Assert.Single(result.Modules[0].Courses);
        Assert.Single(result.Modules[0].Courses[0].Activities);
    }

    [Fact]
    public async Task GetCurriculum_Throws_WhenProgramMissing()
    {
        var sut = CreateSut();

        await Assert.ThrowsAsync<NotFoundException>(() =>
            sut.GetProgramCurriculumAsync(Guid.NewGuid()));
    }

    // ── GetAllProgramsAsync ───────────────────────────────────────────────────

    [Fact]
    public async Task GetAll_ReturnsFilteredSortedPageWithExperts()
    {
        SeedProgram();
        SeedProgram(
            id: _otherProgramId,
            name: "Advanced Program",
            code: "PRG-002");
        SeedExpertOnProgram();
        var sut = CreateSut();

        var result = await sut.GetAllProgramsAsync(
            "steam", "name", false, 1, 10,
            level: DifficultyLevel.Beginner,
            skillsGained: "coding",
            status: ProgramStatus.Active,
            category: ProgramCategory.Technology);

        Assert.Equal(1, result.TotalCount);
        Assert.Equal("STEAM Program", result.Items[0].Name);
        Assert.Single(result.Items[0].Experts);
    }

    [Fact]
    public async Task GetAll_AppliesAlternateSortAndCodeRatingFilters()
    {
        var older = SeedProgram(name: "Alpha", code: "PRG-A");
        older.Rating = 3m;
        older.Price = 50m;
        older.CreatedAt = DateTime.UtcNow.AddDays(-10);

        var newer = SeedProgram(
            id: _otherProgramId,
            name: "Beta",
            code: "PRG-B");
        newer.Rating = 5m;
        newer.Price = 200m;
        newer.CreatedAt = DateTime.UtcNow.AddDays(-1);

        var sut = CreateSut();

        var byRating = await sut.GetAllProgramsAsync(null, "rating", true, 1, 10, rating: 4m);
        var byPrice = await sut.GetAllProgramsAsync(null, "price", false, 1, 10, code: "prg");
        var byCreatedAt = await sut.GetAllProgramsAsync(null, "createdat", true, 1, 10);
        var byName = await sut.GetAllProgramsAsync(null, "name", false, 1, 10);
        var byCode = await sut.GetAllProgramsAsync(null, "code", false, 1, 10);
        var byLevel = await sut.GetAllProgramsAsync(null, "level", true, 1, 10);

        Assert.Equal("Beta", byRating.Items[0].Name);
        Assert.Equal("Alpha", byPrice.Items[0].Name);
        Assert.Equal("Beta", byCreatedAt.Items[0].Name);
        Assert.True(byName.TotalCount >= 1);
        Assert.True(byCode.TotalCount >= 1);
        Assert.True(byLevel.TotalCount >= 1);
    }

    // ── GetAllProgramsWithModulesAsync ────────────────────────────────────────

    [Fact]
    public async Task GetAllWithModules_IncludesModuleList()
    {
        var module = SeedModule();
        SeedProgram(modules: [module]);
        var sut = CreateSut();

        var result = await sut.GetAllProgramsWithModulesAsync(null, null, false, 1, 10);

        Assert.Single(result.Items);
        Assert.Single(result.Items[0].Modules);
        Assert.Equal("MOD-001", result.Items[0].Modules[0].Code);
    }

    // ── CreateProgramAsync ────────────────────────────────────────────────────

    [Fact]
    public async Task Create_PersistsProgram()
    {
        var sut = CreateSut();

        var result = await sut.CreateProgramAsync(new CreateProgramRequestDto
        {
            Code = "PRG-NEW",
            Name = "New Program",
            Category = ProgramCategory.Technology,
            Level = DifficultyLevel.Intermediate,
            Price = 150m,
        });

        Assert.Equal("PRG-NEW", result.Code);
        Assert.Single(_db.Programs.Items);
        Assert.Equal(1, _db.SaveChangesCallCount);
    }

    [Fact]
    public async Task Create_WithThumbnailFile_UploadsAndSetsThumbnailUrl()
    {
        _blobService
            .Setup(b => b.UploadFileAsync(
                It.IsAny<string>(),
                It.IsAny<Stream>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _blobService
            .Setup(b => b.GetPreviewUrlAsync(It.IsAny<string>()))
            .ReturnsAsync("https://cdn.example.com/program-thumbnails/new-created.jpg");

        var sut = CreateSut();

        var result = await sut.CreateProgramAsync(
            new CreateProgramRequestDto
            {
                Code = "PRG-WITH-THUMB",
                Name = "Program With Thumbnail",
                Category = ProgramCategory.Technology,
                Level = DifficultyLevel.Intermediate,
                Price = 150m,
            },
            CreateThumbnailFile());

        Assert.Equal("https://cdn.example.com/program-thumbnails/new-created.jpg", result.ThumbnailUrl);
        Assert.Equal("https://cdn.example.com/program-thumbnails/new-created.jpg", _db.Programs.Items[0].ThumbnailUrl);
    }

    [Fact]
    public async Task Create_Throws_WhenCodeDuplicate()
    {
        SeedProgram();
        var sut = CreateSut();

        await Assert.ThrowsAsync<ConflictException>(() =>
            sut.CreateProgramAsync(new CreateProgramRequestDto
            {
                Code = "prg-001",
                Name = "Duplicate",
                Category = ProgramCategory.Technology,
            }));
    }

    // ── UpdateProgramAsync ────────────────────────────────────────────────────

    [Fact]
    public async Task Update_AppliesChanges()
    {
        SeedProgram();
        var sut = CreateSut();

        var result = await sut.UpdateProgramAsync(_programId, new UpdateProgramRequestDto
        {
            Name = "Updated Program",
            Price = 250m,
        });

        Assert.Equal("Updated Program", result.Name);
        Assert.Equal(250m, result.Price);
        Assert.Equal("Updated Program", _db.Programs.Items[0].Name);
    }

    [Fact]
    public async Task Update_ReturnsUnchanged_WhenNoChanges()
    {
        SeedProgram();
        var sut = CreateSut();

        var result = await sut.UpdateProgramAsync(_programId, new UpdateProgramRequestDto());

        Assert.Equal("STEAM Program", result.Name);
        Assert.Equal(0, _db.SaveChangesCallCount);
    }

    [Fact]
    public async Task Update_Throws_WhenMissingOrCodeDuplicate()
    {
        SeedProgram();
        SeedProgram(id: _otherProgramId, name: "Other", code: "PRG-002");
        var sut = CreateSut();

        await Assert.ThrowsAsync<NotFoundException>(() =>
            sut.UpdateProgramAsync(Guid.NewGuid(), new UpdateProgramRequestDto { Name = "X" }));
        await Assert.ThrowsAsync<ConflictException>(() =>
            sut.UpdateProgramAsync(_programId, new UpdateProgramRequestDto { Code = "PRG-002" }));
    }

    [Fact]
    public async Task UploadThumbnail_UpdatesProgramThumbnailUrl()
    {
        var program = SeedProgram();
        program.ThumbnailUrl = "https://old.example/old.png";
        var sequence = new MockSequence();

        _blobService
            .InSequence(sequence)
            .Setup(b => b.DeleteFileAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _blobService
            .InSequence(sequence)
            .Setup(b => b.UploadFileAsync(
                It.IsAny<string>(),
                It.IsAny<Stream>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _blobService
            .InSequence(sequence)
            .Setup(b => b.GetPreviewUrlAsync(It.IsAny<string>()))
            .ReturnsAsync("https://cdn.example.com/program-thumbnails/new.jpg");

        var sut = CreateSut();

        var result = await sut.UploadProgramThumbnailAsync(_programId, CreateThumbnailFile());

        Assert.Equal("https://cdn.example.com/program-thumbnails/new.jpg", result.ThumbnailUrl);
        Assert.Equal("https://cdn.example.com/program-thumbnails/new.jpg", _db.Programs.Items[0].ThumbnailUrl);
    }

    [Fact]
    public async Task UploadThumbnail_Throws_WhenFileInvalid()
    {
        SeedProgram();
        var sut = CreateSut();

        await Assert.ThrowsAsync<BadRequestException>(() =>
            sut.UploadProgramThumbnailAsync(_programId, CreateThumbnailFile(fileName: "thumb.txt")));
        await Assert.ThrowsAsync<BadRequestException>(() =>
            sut.UploadProgramThumbnailAsync(_programId, CreateThumbnailFile(size: 0)));
    }

    // ── DeleteProgramAsync ────────────────────────────────────────────────────

    [Fact]
    public async Task Delete_SoftDeletesProgram()
    {
        SeedProgram();
        var sut = CreateSut();

        var deleted = await sut.DeleteProgramAsync(_programId);

        Assert.True(deleted);
        Assert.True(_db.Programs.Items[0].IsDeleted);
    }

    [Fact]
    public async Task Delete_Throws_WhenMissing()
    {
        var sut = CreateSut();

        await Assert.ThrowsAsync<NotFoundException>(() => sut.DeleteProgramAsync(_programId));
    }

    [Fact]
    public async Task Update_ThrowsConflict_WhenClassInProgress()
    {
        SeedProgram();
        SeedClass(ClassStatus.InProgress);
        var sut = CreateSut();

        var ex = await Assert.ThrowsAsync<ConflictException>(() =>
            sut.UpdateProgramAsync(_programId, new UpdateProgramRequestDto { Name = "Blocked" }));
        Assert.Contains("in progress", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Delete_ThrowsConflict_WhenClassInProgress()
    {
        SeedProgram();
        SeedClass(ClassStatus.InProgress);
        var sut = CreateSut();

        var ex = await Assert.ThrowsAsync<ConflictException>(() => sut.DeleteProgramAsync(_programId));
        Assert.Contains("in progress", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Update_ThrowsConflict_WhenOpenClassHasActiveEnrollment()
    {
        SeedProgram();
        var classId = SeedClass(ClassStatus.Open);
        _db.ClassEnrollments.Seed(new ClassEnrollment
        {
            Id = Guid.Parse("88888888-8888-8888-8888-888888888888"),
            ClassId = classId,
            StudentId = Guid.Parse("99999999-9999-9999-9999-999999999999"),
            ProgramEnrollmentId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
            Status = ClassEnrollmentStatus.Active,
            IsDeleted = false,
        });
        var sut = CreateSut();

        var ex = await Assert.ThrowsAsync<ConflictException>(() =>
            sut.UpdateProgramAsync(_programId, new UpdateProgramRequestDto { Name = "Blocked" }));
        Assert.Contains("enrolled students", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Update_Allows_WhenOpenClassHasNoEnrollments()
    {
        SeedProgram();
        SeedClass(ClassStatus.Open);
        var sut = CreateSut();

        var result = await sut.UpdateProgramAsync(_programId, new UpdateProgramRequestDto { Name = "Allowed" });
        Assert.Equal("Allowed", result.Name);
    }

    private Guid SeedClass(ClassStatus status)
    {
        var classId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
        _db.Classes.Seed(new Class
        {
            Id = classId,
            Code = "CLS-LOCK",
            Name = "Lock Cohort",
            ProgramId = _programId,
            Status = status,
            MaxCapacity = 20,
            StartDate = DateTime.UtcNow.AddDays(-7),
            EndDate = DateTime.UtcNow.AddDays(60),
            IsDeleted = false,
        });
        return classId;
    }
}
