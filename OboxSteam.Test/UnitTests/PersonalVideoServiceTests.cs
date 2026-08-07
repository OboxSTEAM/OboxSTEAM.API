using System.Text.Json;
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

public sealed class PersonalVideoServiceTests
{
    private readonly Guid _studentId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private readonly Guid _otherStudentId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
    private readonly Guid _mentorId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");
    private readonly Guid _classId = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd");
    private readonly Guid _stackId = Guid.Parse("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee");
    private readonly Guid _itemId = Guid.Parse("ffffffff-ffff-ffff-ffff-ffffffffffff");
    private readonly Guid _mediaId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    private readonly InMemoryUnitOfWork _db = new();
    private readonly Mock<IClaimsService> _claims = new();
    private readonly Mock<IVideoConverterService> _videoConverter = new();
    private readonly Mock<IStrengthMatchService> _strengthMatch = new();
    private readonly Mock<IBlobService> _blob = new();
    private readonly Mock<IPersonalVideoQueue> _queue = new();
    private readonly Mock<INotificationPublisher> _notifications = new();

    private readonly List<NotificationCommand> _published = [];
    private readonly List<PersonalVideoJob> _enqueued = [];

    public PersonalVideoServiceTests()
    {
        _blob.Setup(b => b.BucketName).Returns("obox-bucket");
        _blob.Setup(b => b.GetPreviewUrlAsync(It.IsAny<string>()))
            .ReturnsAsync((string key) => $"https://cdn.example.com/{key}");

        _notifications
            .Setup(n => n.PublishAsync(It.IsAny<NotificationCommand>(), It.IsAny<CancellationToken>()))
            .Callback<NotificationCommand, CancellationToken>((cmd, _) => _published.Add(cmd))
            .Returns(Task.CompletedTask);

        _queue.Setup(q => q.Enqueue(It.IsAny<PersonalVideoJob>()))
            .Callback<PersonalVideoJob>(j => _enqueued.Add(j));

        _videoConverter
            .Setup(v => v.CancelJobAsync(It.IsAny<string>()))
            .Returns(Task.CompletedTask);
        _videoConverter
            .Setup(v => v.GetJobProgressAsync(It.IsAny<string>()))
            .ReturnsAsync(new MediaConvertJobProgress(MediaConvertJobStatus.InProgress, 42));
        _videoConverter
            .Setup(v => v.GetOutputDurationMsAsync(It.IsAny<string>()))
            .ReturnsAsync(60_000L);
    }

    private PersonalVideoService CreateSut(Guid? currentUserId = null)
    {
        _claims.Setup(c => c.GetCurrentUserId).Returns(currentUserId ?? _studentId);
        return new PersonalVideoService(
            _db,
            _videoConverter.Object,
            _strengthMatch.Object,
            _blob.Object,
            _queue.Object,
            _claims.Object,
            _notifications.Object,
            NullLogger<PersonalVideoService>.Instance);
    }

    private void SeedStudentAndClass(Guid? mentorId = null)
    {
        _db.Users.Seed(new User
        {
            Id = _studentId,
            Code = "STU-001",
            Email = "student@test.com",
            FullName = "Student",
            Role = RoleType.Student,
            Status = AccountStatus.Active,
        });
        _db.Users.Seed(new User
        {
            Id = _otherStudentId,
            Code = "STU-002",
            Email = "other@test.com",
            FullName = "Other Student",
            Role = RoleType.Student,
            Status = AccountStatus.Active,
        });
        if (mentorId.HasValue)
        {
            _db.Users.Seed(new User
            {
                Id = mentorId.Value,
                Code = "MNT-001",
                Email = "mentor@test.com",
                FullName = "Mentor",
                Role = RoleType.Mentor,
                Status = AccountStatus.Active,
            });
        }

        _db.Classes.Seed(new Class
        {
            Id = _classId,
            Code = "CLS-001",
            Name = "Test Class",
            ProgramId = Guid.NewGuid(),
            MentorId = mentorId,
            StartDate = DateTime.UtcNow.AddDays(-30),
            EndDate = DateTime.UtcNow.AddDays(30),
            MaxCapacity = 30,
            Status = ClassStatus.InProgress,
        });

        _db.ClassEnrollments.Seed(new ClassEnrollment
        {
            Id = Guid.NewGuid(),
            ClassId = _classId,
            StudentId = _studentId,
            Status = ClassEnrollmentStatus.Active,
        });
    }

    private HighlightVideoStack SeedStack(string strength = "")
    {
        var stack = new HighlightVideoStack
        {
            Id = _stackId,
            ClassId = _classId,
            StudentId = _studentId,
            StrengthDescription = strength,
        };
        _db.HighlightVideoStacks.Seed(stack);
        return stack;
    }

    private HighlightVideoItem SeedItem(
        HighlightVideoStatus status = HighlightVideoStatus.Processing,
        HighlightVideoGenerationKind kind = HighlightVideoGenerationKind.Initial,
        string? jobRef = null,
        Guid? id = null)
    {
        var item = new HighlightVideoItem
        {
            Id = id ?? _itemId,
            StackId = _stackId,
            GenerationKind = kind,
            Status = status,
            RequestedAt = DateTime.UtcNow,
            PersonalVideoJobRef = jobRef,
        };
        _db.HighlightVideoItems.Seed(item);
        return item;
    }

    private MediaAsset SeedTaggedVideo(bool verified = true, Guid? mediaId = null)
    {
        var id = mediaId ?? _mediaId;
        var tag = new MediaTag
        {
            Id = Guid.NewGuid(),
            MediaId = id,
            StudentId = _studentId,
            IsVerified = verified,
            FaceSegmentsJson = JsonSerializer.Serialize(new[]
            {
                new FaceTimestampSegment(1000, 5000),
            }),
        };
        var media = new MediaAsset
        {
            Id = id,
            UploaderId = _mentorId == Guid.Empty ? _studentId : _mentorId,
            ClassId = _classId,
            FileUrl = $"https://obox-bucket.s3.amazonaws.com/media/{id}.mp4",
            FileType = "video",
            VideoStatus = VideoProcessingStatus.TaggingComplete,
            UploadedAt = DateTime.UtcNow,
            MediaConvertJobId = "mc-media-1",
            MediaTags = [tag],
        };
        _db.MediaAssets.Seed(media);
        _db.MediaTags.Seed(tag);
        return media;
    }

    [Fact]
    public async Task ProcessInitialGeneration_EmptyClips_FailsAndPublishesNotification()
    {
        SeedStudentAndClass();
        SeedStack();
        var item = SeedItem();
        var sut = CreateSut();

        await sut.ProcessGenerationAsync(new PersonalVideoJob(
            item.Id,
            PersonalVideoJobKind.InitialGeneration,
            _classId,
            _studentId,
            StrengthDescription: null));

        Assert.Equal(HighlightVideoStatus.Failed, item.Status);
        Assert.Contains("No processed video assets tagged", item.FailureReason);
        Assert.Contains(_published, n => n.Type == NotificationType.HighlightVideoGenerationFailed);
    }

    [Fact]
    public async Task ProcessInitialGeneration_NoStrengthMatch_FailsWithStrengthMessage()
    {
        SeedStudentAndClass(_mentorId);
        SeedStack("coding");
        var item = SeedItem();
        // No tagged media → empty clips with strength message
        var sut = CreateSut();

        await sut.ProcessGenerationAsync(new PersonalVideoJob(
            item.Id,
            PersonalVideoJobKind.InitialGeneration,
            _classId,
            _studentId,
            "coding"));

        Assert.Equal(HighlightVideoStatus.Failed, item.Status);
        Assert.Contains("No video segments matched the specified strengths", item.FailureReason);
        Assert.Contains(_published, n => n.Type == NotificationType.HighlightVideoGenerationFailed);
    }

    [Fact]
    public async Task GetItemProgress_BuildingClips_WhenNoJobRef()
    {
        SeedStudentAndClass();
        SeedStack();
        SeedItem(HighlightVideoStatus.Processing, jobRef: null);
        var sut = CreateSut();

        var progress = await sut.GetItemProgressAsync(_stackId, _itemId);

        Assert.Equal("BuildingClips", progress.Phase);
        Assert.Null(progress.PercentComplete);
        Assert.False(progress.IsTerminal);
    }

    [Fact]
    public async Task GetItemProgress_Encoding_WhenJobRefPresent()
    {
        SeedStudentAndClass();
        SeedStack();
        SeedItem(HighlightVideoStatus.Processing, jobRef: "mc-job-1");
        var sut = CreateSut();

        var progress = await sut.GetItemProgressAsync(_stackId, _itemId);

        Assert.Equal("Encoding", progress.Phase);
        Assert.Equal(42, progress.PercentComplete);
    }

    [Fact]
    public async Task CancelItem_MarksCancelled_AndIgnoresWebhook()
    {
        SeedStudentAndClass();
        SeedStack();
        SeedItem(HighlightVideoStatus.Processing, jobRef: "mc-job-cancel");
        var sut = CreateSut();

        var dto = await sut.CancelItemAsync(_stackId, _itemId);

        Assert.Equal(HighlightVideoStatus.Cancelled, dto.Status);
        Assert.Equal("Cancelled by user.", dto.FailureReason);
        _videoConverter.Verify(v => v.CancelJobAsync("mc-job-cancel"), Times.Once);

        await sut.HandlePersonalVideoJobCompletionAsync("mc-job-cancel", isSuccess: true);

        var item = _db.HighlightVideoItems.Items.Single(i => i.Id == _itemId);
        Assert.Equal(HighlightVideoStatus.Cancelled, item.Status);
        Assert.Null(item.VideoUrl);
    }

    [Fact]
    public async Task RegenerateStack_WhenProcessing_ThrowsConflict()
    {
        SeedStudentAndClass();
        SeedStack();
        SeedItem(HighlightVideoStatus.Processing);
        var sut = CreateSut();

        await Assert.ThrowsAsync<ConflictException>(() => sut.RegenerateStackAsync(_stackId));
    }

    [Fact]
    public async Task RegenerateStack_WhenFull_ThrowsConflict()
    {
        SeedStudentAndClass();
        SeedStack();
        for (var i = 0; i < 4; i++)
        {
            SeedItem(
                HighlightVideoStatus.Completed,
                id: Guid.Parse($"00000000-0000-0000-0000-00000000000{i + 1}"));
        }

        var sut = CreateSut();
        await Assert.ThrowsAsync<ConflictException>(() => sut.RegenerateStackAsync(_stackId));
    }

    [Fact]
    public async Task RegenerateStack_EnqueuesNewInitialItem()
    {
        SeedStudentAndClass();
        SeedStack("leadership");
        SeedItem(HighlightVideoStatus.Completed);
        var sut = CreateSut();

        var result = await sut.RegenerateStackAsync(_stackId);

        Assert.Equal(2, result.ItemCount);
        Assert.True(result.HasProcessingItem);
        Assert.Single(_enqueued);
        Assert.Equal(PersonalVideoJobKind.InitialGeneration, _enqueued[0].Kind);
        Assert.Equal("leadership", _enqueued[0].StrengthDescription);
    }

    [Fact]
    public async Task RetryItem_OnlyInitialFailedOrCancelled()
    {
        SeedStudentAndClass();
        SeedStack();
        SeedItem(HighlightVideoStatus.Failed, HighlightVideoGenerationKind.Trim);
        var sut = CreateSut();

        await Assert.ThrowsAsync<BadRequestException>(() => sut.RetryItemAsync(_stackId, _itemId));
    }

    [Fact]
    public async Task RetryItem_ResetsFailedInitialAndEnqueues()
    {
        SeedStudentAndClass();
        SeedStack("focus");
        var item = SeedItem(HighlightVideoStatus.Failed);
        item.FailureReason = "boom";
        item.VideoUrl = "https://cdn.example.com/old.mp4";
        item.PersonalVideoJobRef = "old-job";
        var sut = CreateSut();

        var dto = await sut.RetryItemAsync(_stackId, _itemId);

        Assert.Equal(HighlightVideoStatus.Processing, dto.Status);
        Assert.Null(dto.FailureReason);
        Assert.Null(dto.VideoUrl);
        Assert.Single(_enqueued);
        Assert.Equal(PersonalVideoJobKind.InitialGeneration, _enqueued[0].Kind);
        Assert.Contains(_published, n => n.Type == NotificationType.HighlightVideoGenerationQueued);
    }

    [Fact]
    public async Task RetryItem_CancelledInitial_Works()
    {
        SeedStudentAndClass();
        SeedStack();
        SeedItem(HighlightVideoStatus.Cancelled);
        var sut = CreateSut();

        var dto = await sut.RetryItemAsync(_stackId, _itemId);

        Assert.Equal(HighlightVideoStatus.Processing, dto.Status);
        Assert.Single(_enqueued);
    }

    [Fact]
    public async Task GetSourceMedia_ReturnsOnlyVerifiedTaggedReadyVideos()
    {
        SeedStudentAndClass(_mentorId);
        SeedStack();
        SeedTaggedVideo(verified: true);

        var unverified = SeedTaggedVideo(verified: false, mediaId: Guid.Parse("22222222-2222-2222-2222-222222222222"));
        unverified.MediaTags.Clear();
        unverified.MediaTags.Add(new MediaTag
        {
            Id = Guid.NewGuid(),
            MediaId = unverified.Id,
            StudentId = _studentId,
            IsVerified = false,
        });

        _db.MediaAssets.Seed(new MediaAsset
        {
            Id = Guid.Parse("33333333-3333-3333-3333-333333333333"),
            UploaderId = _studentId,
            ClassId = _classId,
            FileUrl = "https://obox-bucket.s3.amazonaws.com/media/img.jpg",
            FileType = "image",
            VideoStatus = VideoProcessingStatus.None,
        });

        var sut = CreateSut();
        var result = await sut.GetSourceMediaAsync(_stackId);

        Assert.Single(result);
        Assert.Equal(_mediaId, result[0].MediaId);
        Assert.Single(result[0].FaceSegments);
        Assert.Equal(1000, result[0].FaceSegments[0].StartMs);
        Assert.Equal(5000, result[0].FaceSegments[0].EndMs);
    }

    [Fact]
    public async Task GetStack_StudentCannotAccessAnotherStudentsStack()
    {
        SeedStudentAndClass();
        SeedStack();
        SeedItem(HighlightVideoStatus.Completed);
        var sut = CreateSut(_otherStudentId);

        await Assert.ThrowsAsync<ForbiddenException>(() => sut.GetStackAsync(_stackId));
    }

    [Fact]
    public async Task GetStack_MentorOwnsClass_CanAccess()
    {
        SeedStudentAndClass(_mentorId);
        SeedStack();
        SeedItem(HighlightVideoStatus.Completed);
        var sut = CreateSut(_mentorId);

        var result = await sut.GetStackAsync(_stackId);

        Assert.NotNull(result);
        Assert.Equal(_stackId, result!.Id);
    }

    [Fact]
    public async Task CancelItem_WhenNotProcessing_ThrowsBadRequest()
    {
        SeedStudentAndClass();
        SeedStack();
        SeedItem(HighlightVideoStatus.Completed);
        var sut = CreateSut();

        await Assert.ThrowsAsync<BadRequestException>(() => sut.CancelItemAsync(_stackId, _itemId));
    }

    [Fact]
    public async Task ProcessGeneration_SkipsCancelledItem()
    {
        SeedStudentAndClass();
        SeedStack();
        var item = SeedItem(HighlightVideoStatus.Cancelled);
        var sut = CreateSut();

        await sut.ProcessGenerationAsync(new PersonalVideoJob(
            item.Id,
            PersonalVideoJobKind.InitialGeneration,
            _classId,
            _studentId,
            null));

        Assert.Equal(HighlightVideoStatus.Cancelled, item.Status);
        Assert.DoesNotContain(_published, n => n.Type == NotificationType.HighlightVideoGenerationFailed);
    }
}
