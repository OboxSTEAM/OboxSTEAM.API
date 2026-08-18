using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OboxSteam.Application.DTOs.MaterialDTO;
using OboxSteam.Application.Exceptions;
using OboxSteam.Application.Interfaces;
using OboxSteam.Application.Notifications;
using OboxSteam.Application.Services;
using OboxSteam.Domain.Entities;
using OboxSteam.Domain.Enums;
using OboxSteam.Test.Helpers;

namespace OboxSteam.Test.UnitTests;

public sealed class MaterialServiceTests
{
    private readonly Guid _managerId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private readonly Guid _programId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private readonly Guid _moduleId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private readonly Guid _courseId = Guid.Parse("44444444-4444-4444-4444-444444444444");
    private readonly Guid _activityId = Guid.Parse("55555555-5555-5555-5555-555555555555");
    private readonly Guid _otherActivityId = Guid.Parse("56565656-5656-5656-5656-565656565656");
    private readonly Guid _materialId = Guid.Parse("66666666-6666-6666-6666-666666666666");
    private readonly Guid _classId = Guid.Parse("77777777-7777-7777-7777-777777777777");
    private readonly Guid _enrollmentId = Guid.Parse("88888888-8888-8888-8888-888888888888");

    private readonly InMemoryUnitOfWork _db = new();
    private readonly Mock<IClaimsService> _claimsService = new();
    private readonly Mock<IBlobService> _blobService = new();
    private readonly Mock<IEnrollmentCurriculumService> _enrollmentCurriculum = new();
    private readonly Mock<INotificationPublisher> _notificationPublisher = new();

    private MaterialService CreateSut(Guid? currentUserId = null)
    {
        _claimsService.Setup(c => c.GetCurrentUserId).Returns(currentUserId ?? _managerId);
        _blobService.Setup(b => b.BucketName).Returns("obox-bucket");
        _blobService
            .Setup(b => b.UploadFileAsync(
                It.IsAny<string>(),
                It.IsAny<Stream>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _blobService
            .Setup(b => b.GetPreviewUrlAsync(It.IsAny<string>()))
            .ReturnsAsync("https://cdn.example.com/materials/pdf/file.pdf");
        _blobService
            .Setup(b => b.GetFileUrlAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("https://signed.example.com/materials/pdf/file.pdf");
        _blobService
            .Setup(b => b.DeleteByKeyAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _enrollmentCurriculum
            .Setup(e => e.EnsureActivityAccessibleAsync(_enrollmentId, It.IsAny<Guid>()))
            .Returns(Task.CompletedTask);
        _notificationPublisher
            .Setup(n => n.PublishManyAsync(It.IsAny<IReadOnlyList<NotificationCommand>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        return new MaterialService(
            _claimsService.Object,
            _db,
            _blobService.Object,
            _enrollmentCurriculum.Object,
            _notificationPublisher.Object,
            NullLogger<MaterialService>.Instance);
    }

    private static Mock<IFormFile> CreateFile(
        string fileName = "lesson.pdf",
        long length = 1024)
    {
        var file = new Mock<IFormFile>();
        file.Setup(f => f.FileName).Returns(fileName);
        file.Setup(f => f.Length).Returns(length);
        file.Setup(f => f.OpenReadStream()).Returns(new MemoryStream("pdf"u8.ToArray()));
        return file;
    }

    private (Program Program, Module Module, Course Course, Activity Activity) SeedCurriculum(
        Guid? activityId = null,
        ActivityType activityType = ActivityType.SelfPaced)
    {
        if (_db.Programs.Items.Count == 0)
        {
            _db.Programs.Seed(new Program
            {
                Id = _programId,
                Code = "PRG-001",
                Name = "STEAM Program",
                Category = ProgramCategory.Technology,
                Level = DifficultyLevel.Beginner,
                IsDeleted = false,
            });
        }

        if (_db.Modules.Items.Count == 0)
        {
            _db.Modules.Seed(new Module
            {
                Id = _moduleId,
                Code = "MOD-001",
                Name = "Module A",
                ProgramId = _programId,
                Program = _db.Programs.Items[0],
                ModuleType = ModuleType.Theory,
                ModuleOrder = 1,
                IsDeleted = false,
            });
        }

        if (_db.Courses.Items.Count == 0)
        {
            _db.Courses.Seed(new Course
            {
                Id = _courseId,
                Code = "CRS-001",
                Name = "Intro Course",
                ModuleId = _moduleId,
                Module = _db.Modules.Items[0],
                IsDeleted = false,
            });
        }

        var targetActivityId = activityId ?? _activityId;
        if (_db.Activities.Items.All(a => a.Id != targetActivityId))
        {
            var course = _db.Courses.Items[0];
            _db.Activities.Seed(new Activity
            {
                Id = targetActivityId,
                Code = $"ACT-{targetActivityId.ToString()[..8]}",
                Name = "Lesson",
                CourseId = course.Id,
                Course = course,
                ActivityType = activityType,
                ActivityOrder = _db.Activities.Items.Count + 1,
                IsDeleted = false,
            });
        }

        return (
            _db.Programs.Items[0],
            _db.Modules.Items[0],
            _db.Courses.Items[0],
            _db.Activities.Items.First(a => a.Id == targetActivityId));
    }

    private Material SeedMaterial(
        Guid? id = null,
        Guid? activityId = null,
        string title = "Slides",
        MaterialType materialType = MaterialType.PDF,
        bool isDeleted = false)
    {
        var (_, _, _, activity) = SeedCurriculum(activityId ?? _activityId);
        var material = new Material
        {
            Id = id ?? _materialId,
            ActivityId = activity.Id,
            Activity = activity,
            Title = title,
            MaterialType = materialType,
            FileUrl = "https://obox-bucket.s3.amazonaws.com/materials/pdf/file.pdf",
            FileSizeBytes = 1024,
            CreatedBy = _managerId,
            IsDeleted = isDeleted,
        };
        _db.Materials.Seed(material);
        return material;
    }

    // ── UploadMaterialAsync ───────────────────────────────────────────────────

    [Fact]
    public async Task Upload_PersistsPdfMaterial()
    {
        SeedCurriculum();
        _db.Classes.Seed(new Class
        {
            Id = _classId,
            Code = "CLS-001",
            Name = "Cohort A",
            ProgramId = _programId,
            Status = ClassStatus.Open,
            IsDeleted = false,
        });
        var sut = CreateSut();

        var result = await sut.UploadMaterialAsync(
            CreateFile().Object,
            new UploadMaterialRequestDto
            {
                ActivityId = _activityId,
                Title = "Lesson PDF",
            });

        Assert.Equal("Lesson PDF", result.Title);
        Assert.Equal(MaterialType.PDF, result.MaterialType);
        Assert.Single(_db.Materials.Items);
        _blobService.Verify(b => b.UploadFileAsync(
            It.IsAny<string>(),
            It.IsAny<Stream>(),
            "materials/pdf",
            It.IsAny<CancellationToken>()), Times.Once);
        _notificationPublisher.Verify(n => n.PublishManyAsync(
            It.IsAny<IReadOnlyList<NotificationCommand>>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Upload_Throws_WhenTypeUnsupportedOrOversized()
    {
        SeedCurriculum();
        var sut = CreateSut();

        await Assert.ThrowsAsync<BadRequestException>(() =>
            sut.UploadMaterialAsync(
                CreateFile(fileName: "virus.exe").Object,
                new UploadMaterialRequestDto { ActivityId = _activityId, Title = "Bad" }));
        await Assert.ThrowsAsync<BadRequestException>(() =>
            sut.UploadMaterialAsync(
                CreateFile(length: 51L * 1024 * 1024).Object,
                new UploadMaterialRequestDto { ActivityId = _activityId, Title = "Huge PDF" }));
        await Assert.ThrowsAsync<BadRequestException>(() =>
            sut.UploadMaterialAsync(
                CreateFile(fileName: "photo.png", length: 11L * 1024 * 1024).Object,
                new UploadMaterialRequestDto { ActivityId = _activityId, Title = "Huge image" }));
    }

    [Fact]
    public async Task Upload_UsesDocFolder_ForDocxFiles()
    {
        SeedCurriculum();
        var sut = CreateSut();

        await sut.UploadMaterialAsync(
            CreateFile(fileName: "notes.docx").Object,
            new UploadMaterialRequestDto { ActivityId = _activityId, Title = "Notes" });

        _blobService.Verify(b => b.UploadFileAsync(
            It.IsAny<string>(), It.IsAny<Stream>(), "materials/doc", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Upload_UsesImageFolder_ForPngFiles()
    {
        SeedCurriculum(_otherActivityId);
        var sut = CreateSut();

        await sut.UploadMaterialAsync(
            CreateFile(fileName: "diagram.png").Object,
            new UploadMaterialRequestDto { ActivityId = _otherActivityId, Title = "Diagram" });

        _blobService.Verify(b => b.UploadFileAsync(
            It.IsAny<string>(), It.IsAny<Stream>(), "materials/image", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Upload_UsesVideoFolder_ForMp4Files()
    {
        var videoActivityId = Guid.Parse("58585858-5858-5858-5858-585858585858");
        SeedCurriculum(videoActivityId);
        var sut = CreateSut();

        await sut.UploadMaterialAsync(
            CreateFile(fileName: "clip.mp4").Object,
            new UploadMaterialRequestDto { ActivityId = videoActivityId, Title = "Clip" });

        _blobService.Verify(b => b.UploadFileAsync(
            It.IsAny<string>(), It.IsAny<Stream>(), "materials/video", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Upload_SkipsNotification_WhenNoActiveClass()
    {
        SeedCurriculum();
        var sut = CreateSut();

        await sut.UploadMaterialAsync(
            CreateFile().Object,
            new UploadMaterialRequestDto { ActivityId = _activityId, Title = "Quiet upload" });

        _notificationPublisher.Verify(n => n.PublishManyAsync(
            It.IsAny<IReadOnlyList<NotificationCommand>>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Upload_Throws_WhenActivityMissingNotSelfPacedOrDuplicate()
    {
        var sut = CreateSut();

        await Assert.ThrowsAsync<NotFoundException>(() =>
            sut.UploadMaterialAsync(
                CreateFile().Object,
                new UploadMaterialRequestDto { ActivityId = _activityId, Title = "X" }));

        SeedCurriculum(_otherActivityId, ActivityType.LiveOnline);
        await Assert.ThrowsAsync<BadRequestException>(() =>
            sut.UploadMaterialAsync(
                CreateFile().Object,
                new UploadMaterialRequestDto { ActivityId = _otherActivityId, Title = "X" }));

        SeedMaterial();
        await Assert.ThrowsAsync<ConflictException>(() =>
            sut.UploadMaterialAsync(
                CreateFile().Object,
                new UploadMaterialRequestDto { ActivityId = _activityId, Title = "Dup" }));
    }

    // ── GetAllMaterialsAsync ──────────────────────────────────────────────────

    [Fact]
    public async Task GetAll_ReturnsFilteredSortedPage()
    {
        SeedMaterial();
        var sut = CreateSut();

        var result = await sut.GetAllMaterialsAsync(
            "slides", "title", false, 1, 10,
            materialType: MaterialType.PDF,
            programId: _programId,
            courseId: _courseId,
            activityId: _activityId);

        Assert.Equal(1, result.TotalCount);
        Assert.Equal("Slides", result.Items[0].Title);
        Assert.Equal("Lesson", result.Items[0].ActivityName);
        Assert.Equal("Intro Course", result.Items[0].CourseName);
        Assert.Equal("STEAM Program", result.Items[0].ProgramName);
    }

    [Fact]
    public async Task GetAll_AppliesAlternateSortColumns()
    {
        var first = SeedMaterial(title: "Alpha Slides");
        first.CreatedAt = DateTime.UtcNow.AddDays(-5);
        SeedCurriculum(_otherActivityId);
        var second = SeedMaterial(
            id: Guid.Parse("67676767-6767-6767-6767-676767676767"),
            activityId: _otherActivityId,
            title: "Beta Slides",
            materialType: MaterialType.Image);
        second.CreatedAt = DateTime.UtcNow.AddDays(-1);
        second.Activity!.Name = "Lesson 2";
        var sut = CreateSut();

        var byType = await sut.GetAllMaterialsAsync(null, "materialtype", true, 1, 10);
        var byActivity = await sut.GetAllMaterialsAsync(null, "activityname", true, 1, 10);
        var byUploadedAt = await sut.GetAllMaterialsAsync(null, "uploadedat", true, 1, 10);

        Assert.Equal(MaterialType.Image, byType.Items[0].MaterialType);
        Assert.Equal("Lesson 2", byActivity.Items[0].ActivityName);
        Assert.Equal("Beta Slides", byUploadedAt.Items[0].Title);

        var byCourse = await sut.GetAllMaterialsAsync(null, "coursename", false, 1, 10);
        var byProgram = await sut.GetAllMaterialsAsync(null, "programname", true, 1, 10);
        Assert.Equal("Intro Course", byCourse.Items[0].CourseName);
        Assert.Equal("STEAM Program", byProgram.Items[0].ProgramName);

        var byDefault = await sut.GetAllMaterialsAsync(null, "xxx", false, 1, 10);
        Assert.True(byDefault.TotalCount >= 1);
    }

    // ── GetMaterialByActivityAsync ────────────────────────────────────────────

    [Fact]
    public async Task GetByActivity_ReturnsMaterial()
    {
        SeedMaterial();
        var sut = CreateSut();

        var result = await sut.GetMaterialByActivityAsync(_activityId);

        Assert.NotNull(result);
        Assert.Equal("Slides", result!.Title);
    }

    [Fact]
    public async Task GetByActivity_ReturnsNull_WhenMissing()
    {
        SeedCurriculum();
        var sut = CreateSut();

        Assert.Null(await sut.GetMaterialByActivityAsync(_activityId));
    }

    [Fact]
    public async Task GetByActivity_Throws_WhenActivityNotSelfPaced()
    {
        SeedCurriculum(_otherActivityId, ActivityType.Offline);
        var sut = CreateSut();

        await Assert.ThrowsAsync<BadRequestException>(() =>
            sut.GetMaterialByActivityAsync(_otherActivityId));
    }

    // ── GetMaterialByActivityForEnrollmentAsync ───────────────────────────────

    [Fact]
    public async Task GetByActivityForEnrollment_ReturnsPresignedUrl()
    {
        SeedMaterial();
        var sut = CreateSut();

        var result = await sut.GetMaterialByActivityForEnrollmentAsync(_activityId, _enrollmentId);

        Assert.NotNull(result);
        Assert.Equal("https://signed.example.com/materials/pdf/file.pdf", result!.FileUrl);
        _enrollmentCurriculum.Verify(e =>
            e.EnsureActivityAccessibleAsync(_enrollmentId, _activityId), Times.Once);
    }

    [Fact]
    public async Task GetByActivityForEnrollment_ReturnsNull_WhenNoMaterial()
    {
        SeedCurriculum();
        var sut = CreateSut();

        Assert.Null(await sut.GetMaterialByActivityForEnrollmentAsync(_activityId, _enrollmentId));
    }

    // ── UpdateMaterialAsync ───────────────────────────────────────────────────

    [Fact]
    public async Task Update_ChangesTitle()
    {
        SeedMaterial();
        SeedCurriculum();
        _db.Classes.Seed(new Class
        {
            Id = _classId,
            Code = "CLS-001",
            Name = "Cohort A",
            ProgramId = _programId,
            Status = ClassStatus.InProgress,
            IsDeleted = false,
        });
        var sut = CreateSut();

        var result = await sut.UpdateMaterialAsync(_materialId, new UpdateMaterialRequestDto
        {
            Title = "Updated slides",
        });

        Assert.Equal("Updated slides", result.Title);
        Assert.Equal("Updated slides", _db.Materials.Items[0].Title);
    }

    [Fact]
    public async Task Update_Throws_WhenMissing()
    {
        var sut = CreateSut();

        await Assert.ThrowsAsync<NotFoundException>(() =>
            sut.UpdateMaterialAsync(_materialId, new UpdateMaterialRequestDto { Title = "X" }));
    }

    // ── DeleteMaterialAsync ───────────────────────────────────────────────────

    [Fact]
    public async Task Delete_HardDeletesAndRemovesBlob()
    {
        SeedMaterial();
        var sut = CreateSut();

        await sut.DeleteMaterialAsync(_materialId);

        Assert.Empty(_db.Materials.Items);
        _blobService.Verify(b => b.DeleteByKeyAsync(
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Delete_Throws_WhenBlobDeleteFails()
    {
        var material = SeedMaterial();
        material.FileUrl = "materials/pdf/raw-key.pdf";
        var sut = CreateSut();
        _blobService
            .Setup(b => b.DeleteByKeyAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("S3 unavailable"));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            sut.DeleteMaterialAsync(_materialId));

        Assert.Single(_db.Materials.Items);
    }

    [Fact]
    public async Task Delete_Throws_WhenMissing()
    {
        var sut = CreateSut();

        await Assert.ThrowsAsync<NotFoundException>(() => sut.DeleteMaterialAsync(_materialId));
    }
}
