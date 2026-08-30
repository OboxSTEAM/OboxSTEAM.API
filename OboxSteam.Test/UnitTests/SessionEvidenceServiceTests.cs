using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OboxSteam.Application.Exceptions;
using OboxSteam.Application.Interfaces;
using OboxSteam.Application.Services;
using OboxSteam.Application.Validation;
using OboxSteam.Domain.Entities;
using OboxSteam.Domain.Enums;
using OboxSteam.Test.Helpers;

namespace OboxSteam.Test.UnitTests;

public sealed class SessionEvidenceServiceTests
{
    private readonly Guid _mentorId = Guid.Parse("14141414-1414-1414-1414-141414141414");
    private readonly Guid _classId = Guid.Parse("44444444-4444-4444-4444-444444444444");
    private readonly Guid _sessionId = Guid.Parse("55555555-5555-5555-5555-555555555555");
    private readonly Guid _moduleId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private readonly Guid _programId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private readonly DateTime _now = new(2026, 8, 30, 9, 0, 0, DateTimeKind.Utc);

    private readonly InMemoryUnitOfWork _db = new();
    private readonly Mock<IClaimsService> _claimsService = new();
    private readonly Mock<IBlobService> _blobService = new();

    private SessionEvidenceService CreateSut()
    {
        _claimsService.Setup(c => c.GetCurrentUserId).Returns(_mentorId);
        _blobService
            .Setup(b => b.UploadFileAsync(It.IsAny<string>(), It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _blobService
            .Setup(b => b.GetPreviewUrlAsync(It.IsAny<string>()))
            .ReturnsAsync((string key) => $"https://bucket.s3.amazonaws.com/{key}");

        return new SessionEvidenceService(
            _db,
            _claimsService.Object,
            _blobService.Object,
            NullLogger<SessionEvidenceService>.Instance);
    }

    private void SeedMentorAndSession()
    {
        _db.Users.Seed(new User
        {
            Id = _mentorId,
            Code = "MEN",
            Email = "mentor@test.com",
            Role = RoleType.Mentor,
            IsDeleted = false,
        });
        _db.Classes.Seed(new Class
        {
            Id = _classId,
            Code = "CLS",
            Name = "Cohort",
            ProgramId = _programId,
            MentorId = _mentorId,
            Status = ClassStatus.InProgress,
            MaxCapacity = 20,
            StartDate = _now.AddDays(-1),
            EndDate = _now.AddDays(30),
            IsDeleted = false,
        });
        _db.ClassSessions.Seed(new ClassSession
        {
            Id = _sessionId,
            ClassId = _classId,
            ModuleId = _moduleId,
            Title = "Field trip",
            SessionKind = SessionKind.Offline,
            StartTime = _now,
            EndTime = _now.AddHours(2),
            Status = ClassSessionStatus.InProgress,
            IsDeleted = false,
        });
    }

    private static Mock<IFormFile> CreateImageFile(string fileName = "evidence.jpg", long length = 1024)
    {
        var file = new Mock<IFormFile>();
        file.Setup(f => f.FileName).Returns(fileName);
        file.Setup(f => f.Length).Returns(length);
        file.Setup(f => f.OpenReadStream()).Returns(new MemoryStream([0xFF, 0xD8, 0xFF]));
        return file;
    }

    [Fact]
    public async Task UploadEvidence_StoresUnderSessionFolder_WithoutFaceCheck()
    {
        SeedMentorAndSession();
        var sut = CreateSut();

        var dto = await sut.UploadEvidenceAsync(_sessionId, CreateImageFile().Object);

        Assert.Equal(_sessionId, dto.ClassSessionId);
        Assert.Equal("image", dto.FileType);
        Assert.True(dto.IsReady);
        Assert.Contains($"session-evidence/{_sessionId:D}", dto.FileUrl);

        _blobService.Verify(
            b => b.UploadFileAsync(
                It.IsAny<string>(),
                It.IsAny<Stream>(),
                $"session-evidence/{_sessionId:D}",
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task UploadEvidence_RejectsNonImage()
    {
        SeedMentorAndSession();
        var sut = CreateSut();

        var ex = await Assert.ThrowsAsync<BadRequestException>(
            () => sut.UploadEvidenceAsync(_sessionId, CreateImageFile("clip.mp4").Object));

        Assert.Contains("Only image", ex.Message);
    }

    [Fact]
    public void EnsureMediaEvidencePresent_Throws_WhenRequiredAndMissing()
    {
        var activity = new Activity
        {
            Code = "A1",
            Name = "Trip",
            ActivityType = ActivityType.Offline,
            RequireMediaEvidence = true,
        };

        var ex = Assert.Throws<BadRequestException>(
            () => MentorCompleteValidator.EnsureMediaEvidencePresent(activity, hasSessionImageEvidence: false));

        Assert.Equal(MentorCompleteValidator.MediaEvidenceRequiredMessage, ex.Message);
    }

    [Fact]
    public void EnsureMediaEvidencePresent_Allows_WhenNotRequired()
    {
        var activity = new Activity
        {
            Code = "A1",
            Name = "Trip",
            ActivityType = ActivityType.Offline,
            RequireMediaEvidence = false,
        };

        MentorCompleteValidator.EnsureMediaEvidencePresent(activity, hasSessionImageEvidence: false);
    }
}
