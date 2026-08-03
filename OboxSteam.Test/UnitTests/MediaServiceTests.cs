using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OboxSteam.Application.Exceptions;
using OboxSteam.Application.Interfaces;
using OboxSteam.Application.Notifications;
using OboxSteam.Application.Services;
using OboxSteam.Domain.Entities;
using OboxSteam.Domain.Enums;
using OboxSteam.Test.Helpers;

namespace OboxSteam.Test.UnitTests;

public sealed class MediaServiceTests
{
    private readonly Guid _managerId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private readonly Guid _mentorId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private readonly Guid _studentId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private readonly Guid _programId = Guid.Parse("44444444-4444-4444-4444-444444444444");
    private readonly Guid _classId = Guid.Parse("55555555-5555-5555-5555-555555555555");
    private readonly Guid _sessionId = Guid.Parse("66666666-6666-6666-6666-666666666666");
    private readonly Guid _transcodingId = Guid.Parse("77777777-7777-7777-7777-777777777777");
    private readonly Guid _pendingId = Guid.Parse("88888888-8888-8888-8888-888888888888");
    private readonly Guid _readyId = Guid.Parse("99999999-9999-9999-9999-999999999999");
    private readonly Guid _imageId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");

    private readonly InMemoryUnitOfWork _db = new();
    private readonly Mock<IClaimsService> _claimsService = new();
    private readonly Mock<IBlobService> _blobService = new();
    private readonly Mock<IFaceRecognitionService> _faceRecognition = new();
    private readonly Mock<INotificationPublisher> _notificationPublisher = new();
    private readonly Mock<IVideoConverterService> _videoConverter = new();

    private MediaService CreateSut(Guid? currentUserId = null)
    {
        _claimsService.Setup(c => c.GetCurrentUserId).Returns(currentUserId ?? _managerId);
        _blobService.Setup(b => b.BucketName).Returns("obox-bucket");
        _notificationPublisher
            .Setup(n => n.PublishAsync(It.IsAny<NotificationCommand>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        return new MediaService(
            _claimsService.Object,
            _db,
            _blobService.Object,
            _faceRecognition.Object,
            _notificationPublisher.Object,
            NullLogger<MediaService>.Instance,
            _videoConverter.Object);
    }

    private void SeedBase()
    {
        _db.Users.Seed(
            new User
            {
                Id = _managerId,
                Code = "MGR-001",
                Email = "manager@test.com",
                FullName = "Manager",
                Role = RoleType.Manager,
                IsDeleted = false,
            },
            new User
            {
                Id = _mentorId,
                Code = "MNT-001",
                Email = "mentor@test.com",
                FullName = "Mentor",
                Role = RoleType.Mentor,
                IsDeleted = false,
            },
            new User
            {
                Id = _studentId,
                Code = "STU-001",
                Email = "student@test.com",
                FullName = "Student",
                Role = RoleType.Student,
                IsDeleted = false,
            });

        _db.Programs.Seed(new Program
        {
            Id = _programId,
            Code = "PRG-001",
            Name = "STEAM Program",
            Category = ProgramCategory.Technology,
            Level = DifficultyLevel.Beginner,
            IsDeleted = false,
        });

        _db.Classes.Seed(new Class
        {
            Id = _classId,
            Code = "CLS-001",
            Name = "Cohort A",
            ProgramId = _programId,
            MentorId = _mentorId,
            StartDate = DateTime.UtcNow.AddDays(-7),
            EndDate = DateTime.UtcNow.AddDays(60),
            MaxCapacity = 30,
            Status = ClassStatus.InProgress,
            IsDeleted = false,
        });
    }

    private void SeedMediaAssets()
    {
        var readyTag = new MediaTag
        {
            Id = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"),
            MediaId = _readyId,
            StudentId = _studentId,
            ConfidenceScore = 95m,
            IsVerified = false,
            IsDeleted = false,
        };

        _db.MediaAssets.Seed(
            new MediaAsset
            {
                Id = _transcodingId,
                UploaderId = _mentorId,
                ClassId = _classId,
                ClassSessionId = _sessionId,
                FileType = "video",
                VideoStatus = VideoProcessingStatus.Transcoding,
                MediaConvertJobId = "mc-job-1",
                UploadedAt = DateTime.UtcNow.AddMinutes(-10),
                IsDeleted = false,
                MediaTags = [],
            },
            new MediaAsset
            {
                Id = _pendingId,
                UploaderId = _mentorId,
                ClassId = _classId,
                FileType = "video",
                FileUrl = "https://cdn.example.com/pending.mp4",
                VideoStatus = VideoProcessingStatus.PendingTagging,
                FaceSearchJobId = "rek-job-1",
                UploadedAt = DateTime.UtcNow.AddMinutes(-5),
                IsDeleted = false,
                MediaTags = [],
            },
            new MediaAsset
            {
                Id = _readyId,
                UploaderId = _mentorId,
                ClassId = _classId,
                ClassSessionId = _sessionId,
                FileType = "video",
                FileUrl = "https://cdn.example.com/ready.mp4",
                VideoStatus = VideoProcessingStatus.TaggingComplete,
                UploadedAt = DateTime.UtcNow.AddMinutes(-1),
                IsDeleted = false,
                MediaTags = [readyTag],
            },
            new MediaAsset
            {
                Id = _imageId,
                UploaderId = _mentorId,
                ClassId = _classId,
                FileType = "image",
                FileUrl = "https://cdn.example.com/photo.jpg",
                VideoStatus = VideoProcessingStatus.None,
                UploadedAt = DateTime.UtcNow,
                IsDeleted = false,
                MediaTags = [],
            });

        _db.MediaTags.Seed(readyTag);
    }

    [Fact]
    public async Task GetMediaAsync_Manager_ReturnsAllStatusesIncludingTranscoding()
    {
        SeedBase();
        SeedMediaAssets();
        var sut = CreateSut(_managerId);

        var result = await sut.GetMediaAsync(_classId, null);

        Assert.Equal(4, result.TotalCount);
        Assert.Contains(result.Items, m => m.Id == _transcodingId);
        Assert.Contains(result.Items, m => m.Id == _pendingId);
        Assert.Contains(result.Items, m => m.Id == _readyId);
        Assert.Contains(result.Items, m => m.Id == _imageId);
    }

    [Fact]
    public async Task GetMediaAsync_FilterTaggingComplete_ReturnsOnlyReadyVideosAndImagesWithStatus()
    {
        SeedBase();
        SeedMediaAssets();
        var sut = CreateSut(_managerId);

        var result = await sut.GetMediaAsync(
            _classId,
            null,
            videoStatus: VideoProcessingStatus.TaggingComplete);

        Assert.Single(result.Items);
        Assert.Equal(_readyId, result.Items[0].Id);
    }

    [Fact]
    public async Task GetMediaAsync_FilterFileTypeVideo_ExcludesImages()
    {
        SeedBase();
        SeedMediaAssets();
        var sut = CreateSut(_managerId);

        var result = await sut.GetMediaAsync(_classId, null, fileType: "video");

        Assert.Equal(3, result.TotalCount);
        Assert.All(result.Items, m => Assert.Equal("video", m.FileType));
    }

    [Fact]
    public async Task GetMediaAsync_FilterClassSession_ReturnsMatchingOnly()
    {
        SeedBase();
        SeedMediaAssets();
        var sut = CreateSut(_managerId);

        var result = await sut.GetMediaAsync(_classId, null, classSessionId: _sessionId);

        Assert.Equal(2, result.TotalCount);
        Assert.All(result.Items, m => Assert.Equal(_sessionId, m.ClassSessionId));
    }

    [Fact]
    public async Task GetMediaAsync_Pagination_ReturnsPage()
    {
        SeedBase();
        SeedMediaAssets();
        var sut = CreateSut(_managerId);

        var result = await sut.GetMediaAsync(_classId, null, page: 1, pageSize: 2);

        Assert.Equal(4, result.TotalCount);
        Assert.Equal(2, result.Items.Count);
        Assert.Equal(2, result.TotalPages);
        Assert.True(result.HasNext);
    }

    [Fact]
    public async Task GetMediaAsync_Student_SeesOnlyReadyTaggedMedia()
    {
        SeedBase();
        SeedMediaAssets();
        var sut = CreateSut(_studentId);

        var result = await sut.GetMediaAsync(_classId, null);

        Assert.Single(result.Items);
        Assert.Equal(_readyId, result.Items[0].Id);
        Assert.True(result.Items[0].IsReady);
    }

    [Fact]
    public async Task GetMediaAsync_Mentor_SeesTranscodingInOwnClass()
    {
        SeedBase();
        SeedMediaAssets();
        var sut = CreateSut(_mentorId);

        var result = await sut.GetMediaAsync(_classId, null);

        Assert.Contains(result.Items, m => m.VideoStatus == VideoProcessingStatus.Transcoding);
    }

    [Fact]
    public async Task GetProcessingProgressAsync_Transcoding_ReturnsMediaConvertPercent()
    {
        SeedBase();
        SeedMediaAssets();
        _videoConverter
            .Setup(v => v.GetJobProgressAsync("mc-job-1"))
            .ReturnsAsync(new MediaConvertJobProgress(MediaConvertJobStatus.InProgress, 42));

        var sut = CreateSut(_managerId);
        var result = await sut.GetProcessingProgressAsync(_transcodingId);

        Assert.Equal(VideoProcessingStatus.Transcoding, result.VideoStatus);
        Assert.Equal(42, result.PercentComplete);
        Assert.False(result.IsReady);
        Assert.False(result.IsFailed);
        Assert.Equal("Transcoding", result.StatusLabel);
    }

    [Fact]
    public async Task GetProcessingProgressAsync_PendingTagging_ReturnsNullPercent()
    {
        SeedBase();
        SeedMediaAssets();
        var sut = CreateSut(_managerId);

        var result = await sut.GetProcessingProgressAsync(_pendingId);

        Assert.Equal(VideoProcessingStatus.PendingTagging, result.VideoStatus);
        Assert.Null(result.PercentComplete);
        Assert.False(result.IsReady);
        Assert.Equal("Tagging faces", result.StatusLabel);
        _videoConverter.Verify(v => v.GetJobProgressAsync(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task GetProcessingProgressAsync_TaggingComplete_Returns100()
    {
        SeedBase();
        SeedMediaAssets();
        var sut = CreateSut(_managerId);

        var result = await sut.GetProcessingProgressAsync(_readyId);

        Assert.Equal(VideoProcessingStatus.TaggingComplete, result.VideoStatus);
        Assert.Equal(100, result.PercentComplete);
        Assert.True(result.IsReady);
    }

    [Fact]
    public async Task GetMediaAsync_InvalidPage_ThrowsBadRequest()
    {
        SeedBase();
        var sut = CreateSut(_managerId);

        var ex = await Assert.ThrowsAsync<BadRequestException>(
            () => sut.GetMediaAsync(_classId, null, page: 0, pageSize: 10));

        Assert.Equal(400, ex.StatusCode);
    }
}
