using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OboxSteam.Application.DTOs.ActivityProgressDTO;
using OboxSteam.Application.DTOs.CertificateDTO;
using OboxSteam.Application.Exceptions;
using OboxSteam.Application.Interfaces;
using OboxSteam.Application.Notifications;
using OboxSteam.Application.Services;
using OboxSteam.Application.Validation;
using OboxSteam.Domain.Entities;
using OboxSteam.Domain.Enums;
using OboxSteam.Test.Helpers;

namespace OboxSteam.Test.UnitTests;

public sealed class ActivityProgressServiceTests
{
    private readonly Guid _studentId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private readonly Guid _otherStudentId = Guid.Parse("12121212-1212-1212-1212-121212121212");
    private readonly Guid _managerId = Guid.Parse("13131313-1313-1313-1313-131313131313");
    private readonly Guid _moduleId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private readonly Guid _unlockedModuleId = Guid.Parse("23232323-2323-2323-2323-232323232323");
    private readonly Guid _programId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private readonly Guid _courseId = Guid.Parse("44444444-4444-4444-4444-444444444444");
    private readonly Guid _activityId = Guid.Parse("55555555-5555-5555-5555-555555555555");
    private readonly Guid _enrollmentId = Guid.Parse("66666666-6666-6666-6666-666666666666");
    private readonly Guid _programEnrollmentId = Guid.Parse("77777777-7777-7777-7777-777777777777");
    private readonly Guid _progressId = Guid.Parse("88888888-8888-8888-8888-888888888888");

    private readonly InMemoryUnitOfWork _db = new();
    private readonly Mock<IClaimsService> _claimsService = new();
    private readonly Mock<ICertificateService> _certificateService = new();
    private readonly Mock<INotificationPublisher> _notificationPublisher = new();

    private ActivityProgressService CreateSut(Guid? currentUserId = null)
    {
        _claimsService.Setup(c => c.GetCurrentUserId).Returns(currentUserId ?? _studentId);
        _certificateService
            .Setup(c => c.EnsureProgramCertificateInternalAsync(It.IsAny<Guid>()))
            .ReturnsAsync((CertificateDetailDto?)null);
        _notificationPublisher
            .Setup(n => n.PublishAsync(It.IsAny<NotificationCommand>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _notificationPublisher
            .Setup(n => n.PublishManyAsync(
                It.IsAny<IReadOnlyList<NotificationCommand>>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        return new ActivityProgressService(
            _db,
            _claimsService.Object,
            _certificateService.Object,
            _notificationPublisher.Object,
            NullLogger<ActivityProgressService>.Instance);
    }

    private void SeedStudent()
    {
        _db.Users.Seed(new User
        {
            Id = _studentId,
            Code = "STD-001",
            Email = "student@test.com",
            Role = RoleType.Student,
            IsDeleted = false
        });
    }

    private void SeedManager()
    {
        _db.Users.Seed(new User
        {
            Id = _managerId,
            Code = "MGR-001",
            Email = "manager@test.com",
            Role = RoleType.Manager,
            IsDeleted = false
        });
    }

    private void SeedModule(Guid? moduleId = null, string name = "Module 1", Guid? prerequisiteModuleId = null)
    {
        _db.Modules.Seed(new Module
        {
            Id = moduleId ?? _moduleId,
            Code = "MOD-001",
            Name = name,
            ProgramId = _programId,
            ModuleType = ModuleType.Theory,
            PrerequisiteModuleId = prerequisiteModuleId,
            IsDeleted = false
        });
    }

    private void SeedCourse(Guid? courseId = null, Guid? moduleId = null, bool isDeleted = false)
    {
        _db.Courses.Seed(new Course
        {
            Id = courseId ?? _courseId,
            Code = "CRS-001",
            Name = "Course 1",
            ModuleId = moduleId ?? _moduleId,
            IsDeleted = isDeleted
        });
    }

    private Activity SeedActivity(
        Guid? activityId = null,
        Guid? courseId = null,
        ActivityType type = ActivityType.SelfPaced,
        bool isDeleted = false,
        int order = 1)
    {
        var activity = new Activity
        {
            Id = activityId ?? _activityId,
            Code = "ACT-001",
            Name = "Lesson 1",
            CourseId = courseId ?? _courseId,
            ActivityType = type,
            ActivityOrder = order,
            IsDeleted = isDeleted
        };
        _db.Activities.Seed(activity);
        return activity;
    }

    private void SeedActiveEnrollment(
        Guid? programEnrollmentId = null,
        DateTime? startedAt = null,
        EnrollmentStatus status = EnrollmentStatus.Active,
        int attemptNumber = 1,
        bool isDeleted = false,
        Guid? studentId = null,
        Guid? moduleId = null)
    {
        _db.ModuleEnrollments.Seed(new ModuleEnrollment
        {
            Id = _enrollmentId,
            StudentId = studentId ?? _studentId,
            ModuleId = moduleId ?? _moduleId,
            Status = status,
            ProgramEnrollmentId = programEnrollmentId,
            StartedAt = startedAt,
            AttemptNumber = attemptNumber,
            IsDeleted = isDeleted
        });
    }

    private void SeedProgramEnrollment(DateTime? startedAt = null, bool isDeleted = false)
    {
        _db.ProgramEnrollments.Seed(new ProgramEnrollment
        {
            Id = _programEnrollmentId,
            StudentId = _studentId,
            ProgramId = _programId,
            Status = EnrollmentStatus.Active,
            StartedAt = startedAt,
            IsDeleted = isDeleted
        });
    }

    private ActivityProgress SeedInProgress(
        ActivityStatus status = ActivityStatus.InProgress,
        bool isCompleted = false,
        bool isDeleted = false)
    {
        var progress = new ActivityProgress
        {
            Id = _progressId,
            StudentId = _studentId,
            ActivityId = _activityId,
            ModuleEnrollmentId = _enrollmentId,
            ActivityStatus = status,
            IsCompleted = isCompleted,
            CompletedAt = isCompleted ? DateTime.UtcNow.AddHours(-1) : null,
            IsDeleted = isDeleted
        };
        _db.ActivityProgresses.Seed(progress);
        return progress;
    }

    private void SeedLearningGraph(Guid? programEnrollmentId = null)
    {
        SeedStudent();
        SeedModule();
        SeedCourse();
        SeedActivity();
        SeedActiveEnrollment(programEnrollmentId: programEnrollmentId);
    }

    // ── StartActivityProgressAsync ────────────────────────────────────────────

    [Fact]
    public async Task StartActivityProgress_CreatesInProgress_AndSetsStartedAt()
    {
        SeedLearningGraph(programEnrollmentId: _programEnrollmentId);
        SeedProgramEnrollment();
        var sut = CreateSut();

        var result = await sut.StartActivityProgressAsync(new CreateActivityProgressRequestDto
        {
            ModuleEnrollmentId = _enrollmentId,
            ActivityId = _activityId
        });

        Assert.Equal(_studentId, result.StudentId);
        Assert.Equal(_activityId, result.ActivityId);
        Assert.Equal(_enrollmentId, result.ModuleEnrollmentId);
        Assert.Equal(ActivityStatus.InProgress, result.ActivityStatus);
        Assert.False(result.IsCompleted);
        Assert.Equal("ACT-001", result.ActivityCode);
        Assert.Equal("Lesson 1", result.ActivityName);
        Assert.Equal(ActivityType.SelfPaced, result.ActivityType);
        Assert.Single(_db.ActivityProgresses.Items);
        Assert.NotNull(_db.ModuleEnrollments.Items[0].StartedAt);
        Assert.NotNull(_db.ProgramEnrollments.Items[0].StartedAt);
        Assert.Equal(1, _db.SaveChangesCallCount);
    }

    [Fact]
    public async Task StartActivityProgress_DoesNotOverwriteExistingStartedAt()
    {
        var started = DateTime.UtcNow.AddDays(-3);
        SeedLearningGraph(programEnrollmentId: _programEnrollmentId);
        SeedProgramEnrollment(startedAt: started);
        _db.ModuleEnrollments.Items[0].StartedAt = started;
        var sut = CreateSut();

        await sut.StartActivityProgressAsync(new CreateActivityProgressRequestDto
        {
            ModuleEnrollmentId = _enrollmentId,
            ActivityId = _activityId
        });

        Assert.Equal(started, _db.ModuleEnrollments.Items[0].StartedAt);
        Assert.Equal(started, _db.ProgramEnrollments.Items[0].StartedAt);
    }

    [Fact]
    public async Task StartActivityProgress_ThrowsBadRequest_WhenIdsEmpty()
    {
        SeedStudent();
        var sut = CreateSut();

        await Assert.ThrowsAsync<BadRequestException>(() =>
            sut.StartActivityProgressAsync(new CreateActivityProgressRequestDto
            {
                ModuleEnrollmentId = Guid.Empty,
                ActivityId = _activityId
            }));

        await Assert.ThrowsAsync<BadRequestException>(() =>
            sut.StartActivityProgressAsync(new CreateActivityProgressRequestDto
            {
                ModuleEnrollmentId = _enrollmentId,
                ActivityId = Guid.Empty
            }));
    }

    [Fact]
    public async Task StartActivityProgress_ThrowsForbidden_WhenNotStudent()
    {
        SeedManager();
        var sut = CreateSut(currentUserId: _managerId);

        var ex = await Assert.ThrowsAsync<ForbiddenException>(() =>
            sut.StartActivityProgressAsync(new CreateActivityProgressRequestDto
            {
                ModuleEnrollmentId = _enrollmentId,
                ActivityId = _activityId
            }));

        Assert.Equal("Only students can start activity progress.", ex.Message);
    }

    [Fact]
    public async Task StartActivityProgress_ThrowsNotFound_WhenEnrollmentMissing()
    {
        SeedStudent();
        var sut = CreateSut();

        await Assert.ThrowsAsync<NotFoundException>(() =>
            sut.StartActivityProgressAsync(new CreateActivityProgressRequestDto
            {
                ModuleEnrollmentId = _enrollmentId,
                ActivityId = _activityId
            }));
    }

    [Fact]
    public async Task StartActivityProgress_ThrowsForbidden_WhenEnrollmentBelongsToOtherStudent()
    {
        SeedStudent();
        SeedModule();
        SeedCourse();
        SeedActivity();
        SeedActiveEnrollment(studentId: _otherStudentId);
        var sut = CreateSut();

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            sut.StartActivityProgressAsync(new CreateActivityProgressRequestDto
            {
                ModuleEnrollmentId = _enrollmentId,
                ActivityId = _activityId
            }));
    }

    [Fact]
    public async Task StartActivityProgress_ThrowsBadRequest_WhenEnrollmentNotActive()
    {
        SeedStudent();
        SeedModule();
        SeedCourse();
        SeedActivity();
        SeedActiveEnrollment(status: EnrollmentStatus.Completed);
        var sut = CreateSut();

        await Assert.ThrowsAsync<BadRequestException>(() =>
            sut.StartActivityProgressAsync(new CreateActivityProgressRequestDto
            {
                ModuleEnrollmentId = _enrollmentId,
                ActivityId = _activityId
            }));
    }

    [Fact]
    public async Task StartActivityProgress_ThrowsNotFound_WhenActivityMissing()
    {
        SeedStudent();
        SeedModule();
        SeedActiveEnrollment();
        var sut = CreateSut();

        await Assert.ThrowsAsync<NotFoundException>(() =>
            sut.StartActivityProgressAsync(new CreateActivityProgressRequestDto
            {
                ModuleEnrollmentId = _enrollmentId,
                ActivityId = _activityId
            }));
    }

    [Fact]
    public async Task StartActivityProgress_ThrowsNotFound_WhenCourseDeleted()
    {
        SeedStudent();
        SeedModule();
        SeedCourse(isDeleted: true);
        SeedActivity();
        SeedActiveEnrollment();
        var sut = CreateSut();

        await Assert.ThrowsAsync<NotFoundException>(() =>
            sut.StartActivityProgressAsync(new CreateActivityProgressRequestDto
            {
                ModuleEnrollmentId = _enrollmentId,
                ActivityId = _activityId
            }));
    }

    [Fact]
    public async Task StartActivityProgress_ThrowsBadRequest_WhenActivityNotInModule()
    {
        SeedStudent();
        SeedModule();
        var otherModuleId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        SeedModule(moduleId: otherModuleId, name: "Other");
        SeedCourse(moduleId: otherModuleId);
        SeedActivity();
        SeedActiveEnrollment();
        var sut = CreateSut();

        var ex = await Assert.ThrowsAsync<BadRequestException>(() =>
            sut.StartActivityProgressAsync(new CreateActivityProgressRequestDto
            {
                ModuleEnrollmentId = _enrollmentId,
                ActivityId = _activityId
            }));

        Assert.Equal("Activity does not belong to the enrolled module.", ex.Message);
    }

    [Fact]
    public async Task StartActivityProgress_ThrowsConflict_WhenDuplicate()
    {
        SeedLearningGraph();
        SeedInProgress();
        var sut = CreateSut();

        await Assert.ThrowsAsync<ConflictException>(() =>
            sut.StartActivityProgressAsync(new CreateActivityProgressRequestDto
            {
                ModuleEnrollmentId = _enrollmentId,
                ActivityId = _activityId
            }));
    }

    // ── UpdateActivityProgressAsync ───────────────────────────────────────────

    [Fact]
    public async Task UpdateActivityProgress_WithProgram_CallsCertificate_AndSetsProgramProgress()
    {
        SeedLearningGraph(programEnrollmentId: _programEnrollmentId);
        SeedProgramEnrollment();
        SeedInProgress();
        var sut = CreateSut();

        var result = await sut.UpdateActivityProgressAsync(new UpdateActivityProgressRequestDto
        {
            ModuleEnrollmentId = _enrollmentId,
            ActivityId = _activityId
        });

        Assert.Equal(ActivityStatus.Done, result.ActivityStatus);
        Assert.True(result.IsCompleted);
        Assert.NotNull(result.CompletedAt);
        Assert.Equal(100m, result.ModuleProgressPercent);
        Assert.NotNull(result.ProgramProgressPercent);
        Assert.Equal(EnrollmentStatus.Completed, _db.ModuleEnrollments.Items[0].Status);
        _certificateService.Verify(c => c.EnsureProgramCertificateInternalAsync(_programEnrollmentId), Times.Once);
        _notificationPublisher.Verify(
            n => n.PublishAsync(It.IsAny<NotificationCommand>(), It.IsAny<CancellationToken>()),
            Times.AtLeastOnce);
    }

    [Fact]
    public async Task UpdateActivityProgress_Continues_WhenCertificateThrows()
    {
        SeedLearningGraph(programEnrollmentId: _programEnrollmentId);
        SeedProgramEnrollment();
        SeedInProgress();
        var sut = CreateSut();
        _certificateService
            .Setup(c => c.EnsureProgramCertificateInternalAsync(It.IsAny<Guid>()))
            .ThrowsAsync(new InvalidOperationException("cert failed"));

        var result = await sut.UpdateActivityProgressAsync(new UpdateActivityProgressRequestDto
        {
            ModuleEnrollmentId = _enrollmentId,
            ActivityId = _activityId
        });

        Assert.Equal(ActivityStatus.Done, result.ActivityStatus);
    }

    [Fact]
    public async Task UpdateActivityProgress_PublishesModuleCompleted_AndUnlocked()
    {
        SeedLearningGraph();
        SeedModule(moduleId: _unlockedModuleId, name: "Module 2", prerequisiteModuleId: _moduleId);
        SeedInProgress();
        var sut = CreateSut();

        await sut.UpdateActivityProgressAsync(new UpdateActivityProgressRequestDto
        {
            ModuleEnrollmentId = _enrollmentId,
            ActivityId = _activityId
        });

        _notificationPublisher.Verify(
            n => n.PublishAsync(
                It.Is<NotificationCommand>(c => c.Type == NotificationType.ActivityCompleted),
                It.IsAny<CancellationToken>()),
            Times.Once);
        _notificationPublisher.Verify(
            n => n.PublishAsync(
                It.Is<NotificationCommand>(c => c.Type == NotificationType.ModuleCompleted),
                It.IsAny<CancellationToken>()),
            Times.Once);
        _notificationPublisher.Verify(
            n => n.PublishAsync(
                It.Is<NotificationCommand>(c => c.Type == NotificationType.ModuleUnlocked),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task UpdateActivityProgress_ThrowsForbidden_WhenNotStudent()
    {
        SeedManager();
        var sut = CreateSut(currentUserId: _managerId);

        var ex = await Assert.ThrowsAsync<ForbiddenException>(() =>
            sut.UpdateActivityProgressAsync(new UpdateActivityProgressRequestDto
            {
                ModuleEnrollmentId = _enrollmentId,
                ActivityId = _activityId
            }));

        Assert.Equal("Only students can update activity progress.", ex.Message);
    }

    [Fact]
    public async Task UpdateActivityProgress_ThrowsNotFound_WhenProgressMissing()
    {
        SeedLearningGraph();
        var sut = CreateSut();

        await Assert.ThrowsAsync<NotFoundException>(() =>
            sut.UpdateActivityProgressAsync(new UpdateActivityProgressRequestDto
            {
                ModuleEnrollmentId = _enrollmentId,
                ActivityId = _activityId
            }));
    }

    // ── CompleteActivityForModuleEnrollmentAsync ──────────────────────────────

    [Fact]
    public async Task CompleteActivity_CreatesProgress_WhenNoneExists()
    {
        SeedLearningGraph(programEnrollmentId: _programEnrollmentId);
        SeedProgramEnrollment();
        var sut = CreateSut();

        var result = await sut.CompleteActivityForModuleEnrollmentAsync(
            _enrollmentId,
            _activityId,
            _studentId,
            CompletionSource.Video);

        Assert.Equal(ActivityStatus.Done, result.ActivityStatus);
        Assert.True(result.IsCompleted);
        Assert.Equal(100m, result.ModuleProgressPercent);
        Assert.NotNull(result.ProgramProgressPercent);
        Assert.Single(_db.ActivityProgresses.Items);
        Assert.Equal(CompletionSource.Video, _db.ActivityProgresses.Items[0].CompletionSource);
        Assert.NotNull(_db.ModuleEnrollments.Items[0].StartedAt);
        Assert.NotNull(_db.ProgramEnrollments.Items[0].StartedAt);
        _certificateService.Verify(c => c.EnsureProgramCertificateInternalAsync(_programEnrollmentId), Times.Once);
    }

    [Fact]
    public async Task CompleteActivity_ThrowsForbidden_WhenStudentMismatch()
    {
        SeedLearningGraph();
        var sut = CreateSut();

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            sut.CompleteActivityForModuleEnrollmentAsync(
                _enrollmentId,
                _activityId,
                _otherStudentId));
    }

    // ── SaveCheckpointForModuleEnrollmentAsync ────────────────────────────────

    [Fact]
    public async Task SaveCheckpoint_CreatesAndUpdatesResumeState()
    {
        SeedLearningGraph(programEnrollmentId: _programEnrollmentId);
        SeedProgramEnrollment();
        var sut = CreateSut();

        var created = await sut.SaveCheckpointForModuleEnrollmentAsync(
            _enrollmentId,
            _activityId,
            _studentId,
            """{"kind":"video","positionSeconds":12}""");

        Assert.Equal(ActivityStatus.InProgress, created.ActivityStatus);
        Assert.NotNull(created.ResumeState);
        Assert.Equal("video", created.ResumeState!.Kind);
        Assert.Equal(12, created.ResumeState.PositionSeconds);
        Assert.NotNull(created.LastAccessedAt);
        Assert.NotNull(_db.ModuleEnrollments.Items[0].StartedAt);
        Assert.NotNull(_db.ProgramEnrollments.Items[0].StartedAt);

        var updated = await sut.SaveCheckpointForModuleEnrollmentAsync(
            _enrollmentId,
            _activityId,
            _studentId,
            """{"kind":"pdf","page":3}""");

        Assert.Equal(ActivityStatus.InProgress, updated.ActivityStatus);
        Assert.Equal(3, updated.ResumeState!.Page);
        Assert.Single(_db.ActivityProgresses.Items);
    }

    [Fact]
    public async Task SaveCheckpoint_ThrowsBadRequest_WhenAlreadyCompleted()
    {
        SeedLearningGraph();
        SeedInProgress(status: ActivityStatus.Done, isCompleted: true);
        var sut = CreateSut();

        var ex = await Assert.ThrowsAsync<BadRequestException>(() =>
            sut.SaveCheckpointForModuleEnrollmentAsync(
                _enrollmentId,
                _activityId,
                _studentId,
                """{"kind":"video","positionSeconds":1}"""));

        Assert.Equal("Activity is already completed.", ex.Message);
    }

    [Fact]
    public async Task SaveCheckpoint_ThrowsBadRequest_WhenNotSelfPaced()
    {
        SeedLearningGraph();
        _db.Activities.Items[0].ActivityType = ActivityType.LiveOnline;
        var sut = CreateSut();

        await Assert.ThrowsAsync<BadRequestException>(() =>
            sut.SaveCheckpointForModuleEnrollmentAsync(
                _enrollmentId,
                _activityId,
                _studentId,
                """{"kind":"video","positionSeconds":1}"""));
    }

    // ── ForceCompleteActivityAsync ────────────────────────────────────────────

    [Fact]
    public async Task ForceComplete_CreatesProgress_AndRecalculates()
    {
        SeedLearningGraph(programEnrollmentId: _programEnrollmentId);
        SeedProgramEnrollment();
        var sut = CreateSut();

        var result = await sut.ForceCompleteActivityAsync(_studentId, _activityId);

        Assert.Equal(ActivityStatus.Done, result.ActivityStatus);
        Assert.True(result.IsCompleted);
        Assert.Equal(100m, result.ModuleProgressPercent);
        Assert.NotNull(result.ProgramProgressPercent);
        Assert.Equal(CompletionSource.Manual, _db.ActivityProgresses.Items[0].CompletionSource);
        _certificateService.Verify(c => c.EnsureProgramCertificateInternalAsync(_programEnrollmentId), Times.Once);
    }

    [Fact]
    public async Task ForceComplete_UsesLatestAttemptEnrollment()
    {
        SeedStudent();
        SeedModule();
        SeedCourse();
        SeedActivity();
        _db.ModuleEnrollments.Seed(
            new ModuleEnrollment
            {
                Id = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
                StudentId = _studentId,
                ModuleId = _moduleId,
                Status = EnrollmentStatus.Active,
                AttemptNumber = 1,
                IsDeleted = false
            },
            new ModuleEnrollment
            {
                Id = _enrollmentId,
                StudentId = _studentId,
                ModuleId = _moduleId,
                Status = EnrollmentStatus.Active,
                AttemptNumber = 2,
                IsDeleted = false
            });
        var sut = CreateSut();

        var result = await sut.ForceCompleteActivityAsync(_studentId, _activityId);

        Assert.Equal(_enrollmentId, result.ModuleEnrollmentId);
    }

    [Fact]
    public async Task ForceComplete_ThrowsBadRequest_WhenIdsEmpty()
    {
        var sut = CreateSut();

        await Assert.ThrowsAsync<BadRequestException>(() =>
            sut.ForceCompleteActivityAsync(Guid.Empty, _activityId));

        await Assert.ThrowsAsync<BadRequestException>(() =>
            sut.ForceCompleteActivityAsync(_studentId, Guid.Empty));
    }

    [Fact]
    public async Task ForceComplete_ThrowsNotFound_WhenActivityMissing()
    {
        var sut = CreateSut();

        await Assert.ThrowsAsync<NotFoundException>(() =>
            sut.ForceCompleteActivityAsync(_studentId, _activityId));
    }

    [Fact]
    public async Task ForceComplete_ThrowsNotFound_WhenNoModuleEnrollment()
    {
        SeedCourse();
        SeedActivity();
        var sut = CreateSut();

        var ex = await Assert.ThrowsAsync<NotFoundException>(() =>
            sut.ForceCompleteActivityAsync(_studentId, _activityId));

        Assert.Contains("No module enrollment found", ex.Message);
    }

    // ── MentorCompleteClassSessionAsync ───────────────────────────────────────

    private readonly Guid _mentorId = Guid.Parse("14141414-1414-1414-1414-141414141414");
    private readonly Guid _classId = Guid.Parse("a1a1a1a1-a1a1-a1a1-a1a1-a1a1a1a1a1a1");
    private readonly Guid _sessionId = Guid.Parse("b2b2b2b2-b2b2-b2b2-b2b2-b2b2b2b2b2b2");

    private void SeedMentor()
    {
        _db.Users.Seed(new User
        {
            Id = _mentorId,
            Code = "MNT-001",
            Email = "mentor@test.com",
            Role = RoleType.Mentor,
            IsDeleted = false
        });
    }

    private void SeedClassForMentor(Guid? mentorId = null)
    {
        _db.Classes.Seed(new Class
        {
            Id = _classId,
            Code = "CLS-001",
            Name = "Cohort A",
            ProgramId = _programId,
            MentorId = mentorId ?? _mentorId,
            Status = ClassStatus.InProgress,
            MaxCapacity = 30,
            StartDate = DateTime.UtcNow.AddDays(-7),
            EndDate = DateTime.UtcNow.AddDays(60),
            IsDeleted = false
        });
    }

    private void SeedSessionActivityGraph(ActivityType type = ActivityType.Offline)
    {
        SeedModule();
        SeedCourse();
        SeedActivity(type: type);
        SeedClassForMentor();
        _db.ClassSessions.Seed(new ClassSession
        {
            Id = _sessionId,
            ClassId = _classId,
            ModuleId = _moduleId,
            ActivityId = _activityId,
            Title = "Lab Session",
            SessionKind = SessionKind.LiveOnline,
            StartTime = DateTime.UtcNow.AddHours(-1),
            EndTime = DateTime.UtcNow.AddHours(2),
            RequiresAttendance = true,
            Status = ClassSessionStatus.InProgress,
            IsDeleted = false
        });
    }

    private void SeedRosterStudent(
        Guid studentId,
        Guid programEnrollmentId,
        Guid moduleEnrollmentId,
        AttendanceStatus? attendanceStatus)
    {
        _db.Users.Seed(new User
        {
            Id = studentId,
            Code = $"STD-{studentId.ToString()[..8]}",
            Email = $"{studentId}@test.com",
            Role = RoleType.Student,
            IsDeleted = false
        });
        _db.ProgramEnrollments.Seed(new ProgramEnrollment
        {
            Id = programEnrollmentId,
            StudentId = studentId,
            ProgramId = _programId,
            Status = EnrollmentStatus.Active,
            IsDeleted = false
        });
        _db.ClassEnrollments.Seed(new ClassEnrollment
        {
            Id = Guid.NewGuid(),
            ClassId = _classId,
            StudentId = studentId,
            ProgramEnrollmentId = programEnrollmentId,
            Status = ClassEnrollmentStatus.Active,
            IsDeleted = false
        });
        _db.ModuleEnrollments.Seed(new ModuleEnrollment
        {
            Id = moduleEnrollmentId,
            StudentId = studentId,
            ModuleId = _moduleId,
            ProgramEnrollmentId = programEnrollmentId,
            Status = EnrollmentStatus.Active,
            AttemptNumber = 1,
            IsDeleted = false
        });

        if (attendanceStatus.HasValue)
        {
            _db.SessionAttendances.Seed(new SessionAttendance
            {
                Id = Guid.NewGuid(),
                ClassSessionId = _sessionId,
                StudentId = studentId,
                ModuleEnrollmentId = moduleEnrollmentId,
                Status = attendanceStatus.Value,
                IsDeleted = false
            });
        }
    }

    [Fact]
    public async Task MentorCompleteBulk_CompletesPresentStudents_SkipsAbsent()
    {
        SeedMentor();
        SeedManager();
        SeedSessionActivityGraph(ActivityType.Offline);

        var presentEnrollmentId = Guid.Parse("c1c1c1c1-c1c1-c1c1-c1c1-c1c1c1c1c1c1");
        var absentEnrollmentId = Guid.Parse("c2c2c2c2-c2c2-c2c2-c2c2-c2c2c2c2c2c2");
        var presentPeId = Guid.Parse("d1d1d1d1-d1d1-d1d1-d1d1-d1d1d1d1d1d1");
        var absentPeId = Guid.Parse("d2d2d2d2-d2d2-d2d2-d2d2-d2d2d2d2d2d2");

        SeedRosterStudent(_studentId, presentPeId, presentEnrollmentId, AttendanceStatus.Present);
        SeedRosterStudent(_otherStudentId, absentPeId, absentEnrollmentId, AttendanceStatus.Absent);

        var sut = CreateSut(_managerId);

        var result = await sut.MentorCompleteClassSessionAsync(new MentorCompleteBulkRequestDto
        {
            ClassSessionId = _sessionId,
            ActivityId = _activityId,
        });

        Assert.Equal(2, result.Results.Count);
        var presentResult = Assert.Single(result.Results, r => r.StudentId == _studentId);
        Assert.Equal(MentorCompleteOutcome.Completed, presentResult.Outcome);
        Assert.NotNull(presentResult.Progress);
        Assert.Equal(CompletionSource.Mentor, _db.ActivityProgresses.Items
            .Single(ap => ap.StudentId == _studentId).CompletionSource);

        var absentResult = Assert.Single(result.Results, r => r.StudentId == _otherStudentId);
        Assert.Equal(MentorCompleteOutcome.Skipped, absentResult.Outcome);
        Assert.Contains("Present, Late, or Excused", absentResult.Reason);
    }

    [Theory]
    [InlineData(AttendanceStatus.Late)]
    [InlineData(AttendanceStatus.Excused)]
    public async Task MentorCompleteBulk_AllowsLateAndExcused(AttendanceStatus status)
    {
        SeedManager();
        SeedSessionActivityGraph(ActivityType.LiveOnline);
        SeedRosterStudent(
            _studentId,
            _programEnrollmentId,
            _enrollmentId,
            status);

        var sut = CreateSut(_managerId);

        var result = await sut.MentorCompleteClassSessionAsync(new MentorCompleteBulkRequestDto
        {
            ClassSessionId = _sessionId,
            ActivityId = _activityId,
        });

        Assert.Equal(MentorCompleteOutcome.Completed, Assert.Single(result.Results).Outcome);
    }

    [Fact]
    public async Task MentorCompleteBulk_RejectsSelfPaced()
    {
        SeedManager();
        SeedSessionActivityGraph(ActivityType.SelfPaced);
        SeedRosterStudent(_studentId, _programEnrollmentId, _enrollmentId, AttendanceStatus.Present);
        var sut = CreateSut(_managerId);

        await Assert.ThrowsAsync<BadRequestException>(() =>
            sut.MentorCompleteClassSessionAsync(new MentorCompleteBulkRequestDto
            {
                ClassSessionId = _sessionId,
                ActivityId = _activityId,
            }));
    }

    [Fact]
    public async Task MentorCompleteBulk_RejectsSessionActivityMismatch()
    {
        SeedManager();
        SeedSessionActivityGraph(ActivityType.Offline);
        var otherActivityId = Guid.Parse("e5e5e5e5-e5e5-e5e5-e5e5-e5e5e5e5e5e5");
        SeedActivity(activityId: otherActivityId, type: ActivityType.Offline, order: 2);
        var sut = CreateSut(_managerId);

        await Assert.ThrowsAsync<BadRequestException>(() =>
            sut.MentorCompleteClassSessionAsync(new MentorCompleteBulkRequestDto
            {
                ClassSessionId = _sessionId,
                ActivityId = otherActivityId,
            }));
    }

    [Fact]
    public async Task MentorCompleteBulk_ReportsAlreadyDone()
    {
        SeedManager();
        SeedSessionActivityGraph(ActivityType.Offline);
        SeedRosterStudent(_studentId, _programEnrollmentId, _enrollmentId, AttendanceStatus.Present);
        SeedInProgress(ActivityStatus.Done, isCompleted: true);
        var sut = CreateSut(_managerId);

        var result = await sut.MentorCompleteClassSessionAsync(new MentorCompleteBulkRequestDto
        {
            ClassSessionId = _sessionId,
            ActivityId = _activityId,
        });

        Assert.Equal(MentorCompleteOutcome.AlreadyDone, Assert.Single(result.Results).Outcome);
    }

    [Fact]
    public async Task MentorCompleteBulk_SkipsWhenSequenceLocked()
    {
        SeedManager();
        SeedModule();
        SeedCourse();
        var priorActivityId = Guid.Parse("f1f1f1f1-f1f1-f1f1-f1f1-f1f1f1f1f1f1");
        SeedActivity(activityId: priorActivityId, type: ActivityType.SelfPaced, order: 1);
        SeedActivity(type: ActivityType.Offline, order: 2);
        SeedClassForMentor();
        _db.ClassSessions.Seed(new ClassSession
        {
            Id = _sessionId,
            ClassId = _classId,
            ModuleId = _moduleId,
            ActivityId = _activityId,
            Title = "Lab Session 2",
            SessionKind = SessionKind.LiveOnline,
            StartTime = DateTime.UtcNow.AddHours(-1),
            EndTime = DateTime.UtcNow.AddHours(2),
            RequiresAttendance = true,
            Status = ClassSessionStatus.InProgress,
            IsDeleted = false
        });
        SeedRosterStudent(_studentId, _programEnrollmentId, _enrollmentId, AttendanceStatus.Present);
        var sut = CreateSut(_managerId);

        var result = await sut.MentorCompleteClassSessionAsync(new MentorCompleteBulkRequestDto
        {
            ClassSessionId = _sessionId,
            ActivityId = _activityId,
        });

        var skipped = Assert.Single(result.Results);
        Assert.Equal(MentorCompleteOutcome.Skipped, skipped.Outcome);
        Assert.Equal(CurriculumAccessValidator.ActivityLockedMessage, skipped.Reason);
    }

    [Fact]
    public async Task MentorCompleteBulk_Completes_WhenPriorLiveIncomplete()
    {
        SeedManager();
        SeedModule();
        SeedCourse();
        var priorActivityId = Guid.Parse("f2f2f2f2-f2f2-f2f2-f2f2-f2f2f2f2f2f2");
        SeedActivity(activityId: priorActivityId, type: ActivityType.LiveOnline, order: 1);
        SeedActivity(type: ActivityType.Offline, order: 2);
        SeedClassForMentor();
        _db.ClassSessions.Seed(new ClassSession
        {
            Id = _sessionId,
            ClassId = _classId,
            ModuleId = _moduleId,
            ActivityId = _activityId,
            Title = "Lab Session 2",
            SessionKind = SessionKind.Offline,
            StartTime = DateTime.UtcNow.AddHours(-1),
            EndTime = DateTime.UtcNow.AddHours(2),
            RequiresAttendance = true,
            Status = ClassSessionStatus.InProgress,
            IsDeleted = false
        });
        SeedRosterStudent(_studentId, _programEnrollmentId, _enrollmentId, AttendanceStatus.Present);
        var sut = CreateSut(_managerId);

        var result = await sut.MentorCompleteClassSessionAsync(new MentorCompleteBulkRequestDto
        {
            ClassSessionId = _sessionId,
            ActivityId = _activityId,
        });

        Assert.Equal(MentorCompleteOutcome.Completed, Assert.Single(result.Results).Outcome);
    }

    [Fact]
    public async Task MentorCompleteBulk_ThrowsForbidden_WhenMentorDoesNotOwnClass()
    {
        SeedMentor();
        SeedSessionActivityGraph(ActivityType.Offline);
        _db.Classes.Items.Single().MentorId = Guid.NewGuid();
        SeedRosterStudent(_studentId, _programEnrollmentId, _enrollmentId, AttendanceStatus.Present);
        var sut = CreateSut(_mentorId);

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            sut.MentorCompleteClassSessionAsync(new MentorCompleteBulkRequestDto
            {
                ClassSessionId = _sessionId,
                ActivityId = _activityId,
            }));
    }
}
