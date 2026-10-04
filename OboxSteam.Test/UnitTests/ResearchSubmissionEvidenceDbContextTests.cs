using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OboxSteam.Application.DTOs.MediaDTO;
using OboxSteam.Application.DTOs.ResearchSubmissionDTO;
using OboxSteam.Application.Interfaces;
using OboxSteam.Application.Notifications;
using OboxSteam.Application.Services;
using OboxSteam.Domain.Entities;
using OboxSteam.Domain.Enums;
using OboxSteam.Infrastructure;
using OboxSteam.Test.Helpers;
using OboxSteam.Infrastructure.Persistence;

namespace OboxSteam.Test.UnitTests;

/// <summary>
/// Runs evidence upload/submit against the real <see cref="OboxSteamDbContext"/>, <see cref="UnitOfWork"/>
/// and GenericRepository (EF InMemory provider) so EF change-tracker key conflicts surface.
/// Each step uses a fresh DbContext over the same store, like separate HTTP requests.
/// </summary>
public sealed class ResearchSubmissionEvidenceDbContextTests
{
    private readonly Guid _studentId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private readonly Guid _mentorId = Guid.Parse("14141414-1414-1414-1414-141414141414");
    private readonly Guid _programId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private readonly Guid _researchModuleId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private readonly Guid _classId = Guid.Parse("44444444-4444-4444-4444-444444444444");
    private readonly Guid _milestoneId = Guid.Parse("55555555-5555-5555-5555-555555555555");
    private readonly Guid _assignmentId = Guid.Parse("66666666-6666-6666-6666-666666666666");
    private readonly Guid _moduleEnrollmentId = Guid.Parse("77777777-7777-7777-7777-777777777777");
    private readonly Guid _programEnrollmentId = Guid.Parse("88888888-8888-8888-8888-888888888888");
    private readonly Guid _submissionId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");

    private readonly DbContextOptions<OboxSteamDbContext> _options =
        new DbContextOptionsBuilder<OboxSteamDbContext>()
            .UseInMemoryDatabase($"research-evidence-{Guid.NewGuid()}", o => o.EnableNullChecks(false))
            .Options;

    private readonly Mock<IClaimsService> _claimsService = new();
    private readonly Mock<ICurrentTime> _currentTime = new();
    private readonly Mock<IBlobService> _blobService = new();
    private readonly Mock<IMediaService> _mediaService = new();
    private readonly Mock<ICertificateService> _certificateService = new();
    private readonly Mock<INotificationPublisher> _notificationPublisher = new();

    public ResearchSubmissionEvidenceDbContextTests()
    {
        _claimsService.Setup(c => c.GetCurrentUserId).Returns(_studentId);
        _currentTime.Setup(t => t.GetCurrentTime()).Returns(() => DateTime.UtcNow);
        _blobService.Setup(b => b.BucketName).Returns("oboxsteam-bucket-main");
        _blobService
            .Setup(b => b.GetFileUrlAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string key, CancellationToken _) => $"https://presigned.example.com/{key}");
        _notificationPublisher
            .Setup(n => n.PublishAsync(It.IsAny<NotificationCommand>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
    }

    private OboxSteamDbContext CreateContext() => new(_options);

    private ResearchSubmissionService CreateSut(OboxSteamDbContext context)
    {
        var unitOfWork = new UnitOfWork(
            context,
            _currentTime.Object,
            _claimsService.Object,
            Mock.Of<IServiceProvider>());

        var lifecycle = new ProgramPurchaseLifecycle(
            unitOfWork,
            _currentTime.Object,
            _notificationPublisher.Object,
            NullLogger<ProgramPurchaseLifecycle>.Instance);

        return new ResearchSubmissionService(
            _claimsService.Object,
            unitOfWork,
            _blobService.Object,
            _mediaService.Object,
            _certificateService.Object,
            _notificationPublisher.Object,
            NullLogger<ResearchSubmissionService>.Instance,
            lifecycle,
            new FakeSyncEventPublisher());
    }

    private static IFormFile CreateImageFile()
    {
        var file = new Mock<IFormFile>();
        file.Setup(f => f.FileName).Returns("photo.jpg");
        file.Setup(f => f.Length).Returns(1024);
        file.Setup(f => f.OpenReadStream()).Returns(new MemoryStream([1, 2, 3]));
        return file.Object;
    }

    private void SeedDraftSubmissionContext()
    {
        using var context = CreateContext();
        var now = DateTime.UtcNow;

        context.Users.Add(new User
        {
            Id = _studentId,
            Code = "STD-001",
            Email = "std-001@test.com",
            FullName = "Student One",
            Role = RoleType.Student,
        });
        context.Programs.Add(new Program
        {
            Id = _programId,
            Code = "PRG-001",
            Name = "Research Program",
            Category = ProgramCategory.Technology,
            Level = DifficultyLevel.Beginner,
        });
        context.Modules.Add(new Module
        {
            Id = _researchModuleId,
            Code = "MOD-RSH",
            Name = "Research Module",
            ProgramId = _programId,
            ModuleType = ModuleType.Research,
            ModuleOrder = 1,
        });
        context.Assignments.Add(new Assignment
        {
            Id = _assignmentId,
            Code = "ASG-001",
            Title = "Milestone Deliverable",
            ModuleId = _researchModuleId,
            AssignmentType = AssignmentType.FileUpload,
            MaxPoints = 100,
            PassScore = 70m,
            MaxAttempts = 3,
            TimeLimitMinutes = 60,
            IsRequiredForModulePass = true,
        });
        context.ResearchMilestones.Add(new ResearchMilestone
        {
            Id = _milestoneId,
            Code = "MLS-001",
            Title = "Proposal",
            ModuleId = _researchModuleId,
            MilestoneOrder = 1,
            AssignmentId = _assignmentId,
        });
        context.ProgramEnrollments.Add(new ProgramEnrollment
        {
            Id = _programEnrollmentId,
            StudentId = _studentId,
            ProgramId = _programId,
            Status = EnrollmentStatus.Active,
        });
        context.ModuleEnrollments.Add(new ModuleEnrollment
        {
            Id = _moduleEnrollmentId,
            StudentId = _studentId,
            ModuleId = _researchModuleId,
            ProgramEnrollmentId = _programEnrollmentId,
            Status = EnrollmentStatus.Active,
            AttemptNumber = 1,
        });
        context.Classes.Add(new Class
        {
            Id = _classId,
            Code = "CLS-001",
            Name = "Cohort A",
            ProgramId = _programId,
            MentorId = _mentorId,
            Status = ClassStatus.InProgress,
            Kind = ClassKind.Standard,
            MaxCapacity = 30,
            StartDate = now.AddDays(-7),
            EndDate = now.AddDays(60),
        });
        context.ClassEnrollments.Add(new ClassEnrollment
        {
            Id = Guid.NewGuid(),
            ClassId = _classId,
            StudentId = _studentId,
            ProgramEnrollmentId = _programEnrollmentId,
            Status = ClassEnrollmentStatus.Active,
        });
        context.ClassSessions.Add(new ClassSession
        {
            Id = Guid.NewGuid(),
            ClassId = _classId,
            ModuleId = _researchModuleId,
            AssignmentId = _assignmentId,
            SessionKind = SessionKind.AssignmentWindow,
            Title = "Assignment window",
            StartTime = now.AddDays(-7),
            EndTime = now.AddDays(60),
            RequiresAttendance = false,
            Status = ClassSessionStatus.Scheduled,
        });
        context.Submissions.Add(new Submission
        {
            Id = _submissionId,
            Code = "SUB-001",
            AssignmentId = _assignmentId,
            StudentId = _studentId,
            ModuleEnrollmentId = _moduleEnrollmentId,
            ResearchMilestoneId = _milestoneId,
            AttemptNumber = 0,
            Status = SubmissionStatus.Pending,
        });
        context.SaveChanges();
    }

    private Guid SeedMediaAsset(string fileName)
    {
        using var context = CreateContext();
        var media = new MediaAsset
        {
            Id = Guid.NewGuid(),
            UploaderId = _studentId,
            ClassId = _classId,
            FileUrl = $"https://cdn.example.com/media/{fileName}",
            FileType = "image",
            VideoStatus = VideoProcessingStatus.None,
        };
        context.MediaAssets.Add(media);
        context.SaveChanges();
        return media.Id;
    }

    private void SeedEvidenceLink(Guid mediaId, bool isDeleted = false)
    {
        using var context = CreateContext();
        context.SubmissionEvidences.Add(new SubmissionEvidence
        {
            SubmissionId = _submissionId,
            MediaId = mediaId,
            IsDeleted = isDeleted,
            DeletedAt = isDeleted ? DateTime.UtcNow : null,
        });
        context.SaveChanges();
    }

    private void SetupMediaUpload(Guid mediaId)
    {
        _mediaService
            .Setup(m => m.UploadMediaAsync(It.IsAny<IFormFile>(), _classId, null))
            .ReturnsAsync(new MediaAssetDto
            {
                Id = mediaId,
                UploaderId = _studentId,
                ClassId = _classId,
                FileUrl = "https://cdn.example.com/media/evidence.jpg",
                FileType = "image",
                VideoStatus = VideoProcessingStatus.None,
                IsReady = true,
            });
    }

    private async Task<ResearchSubmissionResponseDto> SubmitWithEvidence(params Guid[] mediaIds)
    {
        await using var context = CreateContext();
        return await CreateSut(context).SubmitResearchWork(new SubmitResearchWorkRequestDto
        {
            ModuleEnrollmentId = _moduleEnrollmentId,
            ResearchMilestoneId = _milestoneId,
            ContentText = "Proposal with evidence",
            EvidenceMediaAssetIds = [.. mediaIds],
        });
    }

    private List<SubmissionEvidence> LoadAllLinks()
    {
        using var context = CreateContext();
        return context.SubmissionEvidences
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(se => se.SubmissionId == _submissionId)
            .ToList();
    }

    private MediaAsset LoadMedia(Guid mediaId)
    {
        using var context = CreateContext();
        return context.MediaAssets.IgnoreQueryFilters().AsNoTracking().Single(m => m.Id == mediaId);
    }

    [Fact]
    public async Task SubmitResearchWork_Succeeds_WhenSubmittingSameMediaAsUploadedEvidence()
    {
        SeedDraftSubmissionContext();
        var mediaId = SeedMediaAsset("evidence.jpg");
        SetupMediaUpload(mediaId);

        using (var uploadContext = CreateContext())
        {
            var upload = await CreateSut(uploadContext).UploadSubmissionFile(
                _moduleEnrollmentId,
                _milestoneId,
                CreateImageFile(),
                isEvidence: true);
            Assert.Equal(mediaId, upload.MediaAssetId);
        }

        var result = await SubmitWithEvidence(mediaId);

        Assert.Equal(SubmissionStatus.TurnedIn, result.Status);
        Assert.Equal([mediaId], result.EvidenceMediaAssetIds);
        var link = Assert.Single(LoadAllLinks());
        Assert.Equal(mediaId, link.MediaId);
        Assert.False(link.IsDeleted);
        Assert.False(LoadMedia(mediaId).IsDeleted);
    }

    [Fact]
    public async Task SubmitResearchWork_KeepsDropsAndAddsEvidence_AgainstExistingLinks()
    {
        SeedDraftSubmissionContext();
        var keptId = SeedMediaAsset("kept.jpg");
        var droppedId = SeedMediaAsset("dropped.jpg");
        var addedId = SeedMediaAsset("added.jpg");
        SeedEvidenceLink(keptId);
        SeedEvidenceLink(droppedId);

        var result = await SubmitWithEvidence(keptId, addedId);

        Assert.Equal(SubmissionStatus.TurnedIn, result.Status);
        Assert.Equal(
            new HashSet<Guid> { keptId, addedId },
            result.EvidenceMediaAssetIds.ToHashSet());

        var links = LoadAllLinks().ToDictionary(se => se.MediaId);
        Assert.Equal(3, links.Count);
        Assert.False(links[keptId].IsDeleted);
        Assert.False(links[addedId].IsDeleted);
        Assert.True(links[droppedId].IsDeleted);
        Assert.False(LoadMedia(keptId).IsDeleted);
        Assert.False(LoadMedia(addedId).IsDeleted);
        Assert.True(LoadMedia(droppedId).IsDeleted);
    }

    [Fact]
    public async Task SubmitResearchWork_RestoresSoftDeletedLink_WhenMediaIsSubmittedAgain()
    {
        SeedDraftSubmissionContext();
        var mediaId = SeedMediaAsset("restored.jpg");
        SeedEvidenceLink(mediaId, isDeleted: true);

        var result = await SubmitWithEvidence(mediaId);

        Assert.Equal([mediaId], result.EvidenceMediaAssetIds);
        var link = Assert.Single(LoadAllLinks());
        Assert.False(link.IsDeleted);
        Assert.Null(link.DeletedAt);
        Assert.Null(link.DeletedBy);
    }
}
