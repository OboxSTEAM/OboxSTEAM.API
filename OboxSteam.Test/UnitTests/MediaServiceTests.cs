using Microsoft.AspNetCore.Http;
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
    private readonly Guid _outsideStudentId = Guid.Parse("33333333-3333-3333-3333-333333333334");
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
            },
            new User
            {
                Id = _outsideStudentId,
                Code = "STU-002",
                Email = "outside@test.com",
                FullName = "Outside Student",
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

    private void SeedActiveEnrollment(Guid studentId, Guid? classId = null)
    {
        _db.ClassEnrollments.Seed(new ClassEnrollment
        {
            Id = Guid.NewGuid(),
            ClassId = classId ?? _classId,
            StudentId = studentId,
            ProgramEnrollmentId = Guid.NewGuid(),
            Status = ClassEnrollmentStatus.Active,
            EnrolledAt = DateTime.UtcNow.AddDays(-1),
            IsDeleted = false,
        });
    }

    private static Mock<IFormFile> CreateImageFile(string fileName = "photo.jpg", long length = 1024)
    {
        var file = new Mock<IFormFile>();
        file.Setup(f => f.FileName).Returns(fileName);
        file.Setup(f => f.Length).Returns(length);
        file.Setup(f => f.OpenReadStream()).Returns(new MemoryStream([0xFF, 0xD8, 0xFF]));
        return file;
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

    [Fact]
    public async Task UploadMediaAsync_Image_TagsOnlyActiveEnrolledStudents()
    {
        SeedBase();
        SeedActiveEnrollment(_studentId);
        _blobService
            .Setup(b => b.UploadFileAsync(
                It.IsAny<string>(),
                It.IsAny<Stream>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _blobService
            .Setup(b => b.GetPreviewUrlAsync(It.IsAny<string>()))
            .ReturnsAsync("https://cdn.example.com/photo.jpg");
        _faceRecognition
            .Setup(f => f.SearchFacesAsync("obox-bucket", It.IsAny<string>(), It.IsAny<float>()))
            .ReturnsAsync(
            [
                new FaceMatchResult(_studentId, "face-in", 98f),
                new FaceMatchResult(_outsideStudentId, "face-out", 97f),
            ]);

        var sut = CreateSut(_mentorId);
        var result = await sut.UploadMediaAsync(CreateImageFile().Object, _classId);

        Assert.Single(result.Tags);
        Assert.Equal(_studentId, result.Tags[0].StudentId);
        Assert.DoesNotContain(result.Tags, t => t.StudentId == _outsideStudentId);
        Assert.Single(_db.MediaTags.Items, t => !t.IsDeleted);
    }

    [Fact]
    public async Task UploadMediaAsync_Image_OutOfClassOnly_AcceptsWithZeroTags()
    {
        SeedBase();
        _blobService
            .Setup(b => b.UploadFileAsync(
                It.IsAny<string>(),
                It.IsAny<Stream>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _blobService
            .Setup(b => b.GetPreviewUrlAsync(It.IsAny<string>()))
            .ReturnsAsync("https://cdn.example.com/photo.jpg");
        _faceRecognition
            .Setup(f => f.SearchFacesAsync("obox-bucket", It.IsAny<string>(), It.IsAny<float>()))
            .ReturnsAsync([new FaceMatchResult(_outsideStudentId, "face-out", 97f)]);

        var sut = CreateSut(_mentorId);
        var result = await sut.UploadMediaAsync(CreateImageFile().Object, _classId);

        Assert.Equal("image", result.FileType);
        Assert.Empty(result.Tags);
        Assert.DoesNotContain(_db.MediaTags.Items, t => !t.IsDeleted);
    }

    [Fact]
    public async Task TryProcessVideoTagsAsync_SkipsStudentsNotActiveInClass()
    {
        SeedBase();
        SeedActiveEnrollment(_studentId);
        SeedMediaAssets();
        _faceRecognition
            .Setup(f => f.GetVideoFaceSearchResultsAsync("rek-job-1"))
            .ReturnsAsync(new VideoFaceSearchResult(
                "SUCCEEDED",
                [
                    new FaceMatchResult(_studentId, "face-in", 96f),
                    new FaceMatchResult(_outsideStudentId, "face-out", 95f),
                ]));
        _faceRecognition
            .Setup(f => f.GetAllFaceTimelinesAsync("rek-job-1"))
            .ReturnsAsync(new Dictionary<Guid, VideoFaceTimelineResult>
            {
                [_studentId] = new VideoFaceTimelineResult(true, [new FaceTimestampSegment(0, 1000)]),
                [_outsideStudentId] = new VideoFaceTimelineResult(true, [new FaceTimestampSegment(0, 1000)]),
            });

        var sut = CreateSut(_managerId);
        var done = await sut.TryProcessVideoTagsAsync(_pendingId);

        Assert.True(done);
        var tags = _db.MediaTags.Items.Where(t => t.MediaId == _pendingId && !t.IsDeleted).ToList();
        Assert.Single(tags);
        Assert.Equal(_studentId, tags[0].StudentId);
        var pending = _db.MediaAssets.Items.Single(m => m.Id == _pendingId);
        Assert.Equal(VideoProcessingStatus.TaggingComplete, pending.VideoStatus);
    }

    [Fact]
    public async Task GetClassGalleryAsync_Student_ReturnsAllStatusesWithoutTags()
    {
        SeedBase();
        SeedActiveEnrollment(_studentId);
        SeedMediaAssets();
        var sut = CreateSut(_studentId);

        var result = await sut.GetClassGalleryAsync(_classId);

        Assert.Equal(4, result.TotalCount);
        Assert.Contains(result.Items, m => m.Id == _transcodingId);
        Assert.Contains(result.Items, m => m.Id == _pendingId);
        Assert.Contains(result.Items, m => m.Id == _readyId);
        Assert.Contains(result.Items, m => m.Id == _imageId);
        Assert.All(result.Items, m => Assert.Equal(_classId, m.ClassId));
    }

    [Fact]
    public async Task GetClassGalleryAsync_Student_FilterFileTypeAndPagination()
    {
        SeedBase();
        SeedActiveEnrollment(_studentId);
        SeedMediaAssets();
        var sut = CreateSut(_studentId);

        var result = await sut.GetClassGalleryAsync(_classId, fileType: "video", page: 1, pageSize: 2);

        Assert.Equal(3, result.TotalCount);
        Assert.Equal(2, result.Items.Count);
        Assert.All(result.Items, m => Assert.Equal("video", m.FileType));
    }

    [Fact]
    public async Task GetMediaAsync_Manager_IncludesResearchEvidenceMedia()
    {
        SeedBase();
        SeedActiveEnrollment(_studentId);
        SeedMediaAssets();
        var evidenceMediaId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
        _db.MediaAssets.Seed(new MediaAsset
        {
            Id = evidenceMediaId,
            UploaderId = _studentId,
            ClassId = _classId,
            FileType = "video",
            FileUrl = "https://cdn.example.com/media/evidence.mp4",
            VideoStatus = VideoProcessingStatus.Transcoding,
            UploadedAt = DateTime.UtcNow,
            IsDeleted = false,
        });
        _db.SubmissionEvidences.Seed(new SubmissionEvidence
        {
            SubmissionId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc"),
            MediaId = evidenceMediaId,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = _studentId,
            IsDeleted = false,
        });
        var sut = CreateSut(_managerId);

        var result = await sut.GetMediaAsync(_classId, null);

        Assert.Contains(result.Items, m => m.Id == evidenceMediaId);
    }

    [Fact]
    public async Task GetClassGalleryAsync_ExcludesResearchSubmissionEvidenceLinks()
    {
        SeedBase();
        SeedActiveEnrollment(_studentId);
        SeedMediaAssets();
        var evidenceMediaId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
        _db.MediaAssets.Seed(new MediaAsset
        {
            Id = evidenceMediaId,
            UploaderId = _studentId,
            ClassId = _classId,
            FileType = "image",
            FileUrl = "https://cdn.example.com/media/evidence.jpg",
            VideoStatus = VideoProcessingStatus.None,
            UploadedAt = DateTime.UtcNow,
            IsDeleted = false,
        });
        _db.SubmissionEvidences.Seed(new SubmissionEvidence
        {
            SubmissionId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc"),
            MediaId = evidenceMediaId,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = _studentId,
            IsDeleted = false,
        });
        var sut = CreateSut(_studentId);

        var result = await sut.GetClassGalleryAsync(_classId);

        Assert.Equal(4, result.TotalCount);
        Assert.DoesNotContain(result.Items, m => m.Id == evidenceMediaId);
    }

    [Fact]
    public async Task GetClassGalleryAsync_Mentor_ThrowsForbidden()
    {
        SeedBase();
        SeedActiveEnrollment(_studentId);
        SeedMediaAssets();
        var sut = CreateSut(_mentorId);

        var ex = await Assert.ThrowsAsync<ForbiddenException>(
            () => sut.GetClassGalleryAsync(_classId));

        Assert.Equal(403, ex.StatusCode);
    }

    [Fact]
    public async Task GetClassGalleryAsync_StudentNotEnrolled_ThrowsForbidden()
    {
        SeedBase();
        SeedMediaAssets();
        var sut = CreateSut(_studentId);

        var ex = await Assert.ThrowsAsync<ForbiddenException>(
            () => sut.GetClassGalleryAsync(_classId));

        Assert.Equal(403, ex.StatusCode);
    }

    [Fact]
    public async Task GetMyGalleryAsync_ReturnsMediaFromAllEnrolledClasses()
    {
        SeedBase();
        var class2Id = Guid.Parse("55555555-5555-5555-5555-555555555556");
        var image2Id = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaab");
        _db.Classes.Seed(new Class
        {
            Id = class2Id,
            Code = "CLS-002",
            Name = "Cohort B",
            ProgramId = _programId,
            MentorId = _mentorId,
            StartDate = DateTime.UtcNow.AddDays(-7),
            EndDate = DateTime.UtcNow.AddDays(60),
            MaxCapacity = 30,
            Status = ClassStatus.InProgress,
            IsDeleted = false,
        });
        SeedActiveEnrollment(_studentId, _classId);
        SeedActiveEnrollment(_studentId, class2Id);
        SeedMediaAssets();
        _db.MediaAssets.Seed(new MediaAsset
        {
            Id = image2Id,
            UploaderId = _mentorId,
            ClassId = class2Id,
            FileType = "image",
            FileUrl = "https://cdn.example.com/class2.jpg",
            VideoStatus = VideoProcessingStatus.None,
            UploadedAt = DateTime.UtcNow,
            IsDeleted = false,
            MediaTags = [],
        });
        var sut = CreateSut(_studentId);

        var result = await sut.GetMyGalleryAsync();

        Assert.Equal(5, result.TotalCount);
        Assert.Contains(result.Items, m => m.Id == _imageId && m.ClassName == "Cohort A");
        Assert.Contains(result.Items, m => m.Id == image2Id && m.ClassName == "Cohort B");
        Assert.All(result.Items, m =>
        {
            Assert.Equal(_programId, m.ProgramId);
            Assert.Equal("STEAM Program", m.ProgramName);
        });
    }

    [Fact]
    public async Task GetMyGalleryAsync_FilterByProgramAndClass()
    {
        SeedBase();
        var otherProgramId = Guid.Parse("44444444-4444-4444-4444-444444444445");
        var otherClassId = Guid.Parse("55555555-5555-5555-5555-555555555557");
        var otherImageId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaac");

        _db.Programs.Seed(new Program
        {
            Id = otherProgramId,
            Code = "PRG-002",
            Name = "Art Program",
            Category = ProgramCategory.Art,
            Level = DifficultyLevel.Beginner,
            IsDeleted = false,
        });
        _db.Classes.Seed(new Class
        {
            Id = otherClassId,
            Code = "CLS-003",
            Name = "Art Cohort",
            ProgramId = otherProgramId,
            MentorId = _mentorId,
            StartDate = DateTime.UtcNow.AddDays(-7),
            EndDate = DateTime.UtcNow.AddDays(60),
            MaxCapacity = 30,
            Status = ClassStatus.InProgress,
            IsDeleted = false,
        });
        SeedActiveEnrollment(_studentId, _classId);
        SeedActiveEnrollment(_studentId, otherClassId);
        SeedMediaAssets();
        _db.MediaAssets.Seed(new MediaAsset
        {
            Id = otherImageId,
            UploaderId = _mentorId,
            ClassId = otherClassId,
            FileType = "image",
            FileUrl = "https://cdn.example.com/art.jpg",
            VideoStatus = VideoProcessingStatus.None,
            UploadedAt = DateTime.UtcNow,
            IsDeleted = false,
            MediaTags = [],
        });
        var sut = CreateSut(_studentId);

        var byProgram = await sut.GetMyGalleryAsync(programId: _programId);
        Assert.Equal(4, byProgram.TotalCount);
        Assert.DoesNotContain(byProgram.Items, m => m.Id == otherImageId);

        var byClass = await sut.GetMyGalleryAsync(classId: otherClassId);
        Assert.Equal(1, byClass.TotalCount);
        Assert.Equal(otherImageId, byClass.Items[0].Id);
    }

    [Fact]
    public async Task GetMyGalleryAsync_ClassNotEnrolled_ThrowsForbidden()
    {
        SeedBase();
        SeedMediaAssets();
        var sut = CreateSut(_studentId);

        var ex = await Assert.ThrowsAsync<ForbiddenException>(
            () => sut.GetMyGalleryAsync(classId: _classId));

        Assert.Equal(403, ex.StatusCode);
    }

    [Fact]
    public async Task AddMediaTagAsync_VideoWithExistingTag_PersistsEmptyFaceTimeline()
    {
        SeedBase();
        SeedMediaAssets();
        SeedActiveEnrollment(_outsideStudentId);
        var existingTag = _db.MediaTags.Items.Single(t => t.MediaId == _readyId && t.StudentId == _studentId);
        existingTag.FaceSegmentsJson = "[{\"StartMs\":1000,\"EndMs\":4000}]";
        existingTag.HasOtherFaces = false;
        var sut = CreateSut(_managerId);

        var dto = await sut.AddMediaTagAsync(_readyId, _outsideStudentId);

        Assert.Equal(_outsideStudentId, dto.StudentId);
        Assert.True(dto.IsVerified);

        var newTag = _db.MediaTags.Items.Single(t => t.MediaId == _readyId && t.StudentId == _outsideStudentId);
        Assert.Equal("[]", newTag.FaceSegmentsJson);
        Assert.True(newTag.HasOtherFaces);

        // Other student's timeline must remain unchanged.
        Assert.Equal("[{\"StartMs\":1000,\"EndMs\":4000}]", existingTag.FaceSegmentsJson);
        Assert.False(existingTag.HasOtherFaces);
    }

    [Fact]
    public async Task AddMediaTagAsync_Image_DoesNotSetFaceTimeline()
    {
        SeedBase();
        SeedMediaAssets();
        SeedActiveEnrollment(_studentId);
        var sut = CreateSut(_managerId);

        await sut.AddMediaTagAsync(_imageId, _studentId);

        var tag = _db.MediaTags.Items.Single(t => t.MediaId == _imageId && t.StudentId == _studentId);
        Assert.Null(tag.FaceSegmentsJson);
        Assert.False(tag.HasOtherFaces);
    }
}
