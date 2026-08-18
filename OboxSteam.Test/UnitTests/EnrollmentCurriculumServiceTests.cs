using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OboxSteam.Application.DTOs.ActivityProgressDTO;
using OboxSteam.Application.DTOs.EnrollmentDTO;
using OboxSteam.Application.Exceptions;
using OboxSteam.Application.Interfaces;
using OboxSteam.Application.Services;
using OboxSteam.Domain.Entities;
using OboxSteam.Domain.Enums;
using OboxSteam.Test.Helpers;

namespace OboxSteam.Test.UnitTests;

public sealed class EnrollmentCurriculumServiceTests
{
    private readonly Guid _studentId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private readonly Guid _otherStudentId = Guid.Parse("12121212-1212-1212-1212-121212121212");
    private readonly Guid _managerId = Guid.Parse("13131313-1313-1313-1313-131313131313");
    private readonly Guid _programId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private readonly Guid _programEnrollmentId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private readonly Guid _theoryModuleId = Guid.Parse("44444444-4444-4444-4444-444444444444");
    private readonly Guid _researchModuleId = Guid.Parse("45454545-4545-4545-4545-454545454545");
    private readonly Guid _courseId = Guid.Parse("55555555-5555-5555-5555-555555555555");
    private readonly Guid _activity1Id = Guid.Parse("66666666-6666-6666-6666-666666666666");
    private readonly Guid _activity2Id = Guid.Parse("67676767-6767-6767-6767-676767676767");
    private readonly Guid _liveActivityId = Guid.Parse("68686868-6868-6868-6868-686868686868");
    private readonly Guid _materialId = Guid.Parse("69696969-6969-6969-6969-696969696969");
    private readonly Guid _moduleAssignmentId = Guid.Parse("6a6a6a6a-6a6a-6a6a-6a6a-6a6a6a6a6a6a");
    private readonly Guid _courseAssignmentId = Guid.Parse("6b6b6b6b-6b6b-6b6b-6b6b-6b6b6b6b6b6b");
    private readonly Guid _milestoneId = Guid.Parse("77777777-7777-7777-7777-777777777777");
    private readonly Guid _milestoneAssignmentId = Guid.Parse("78787878-7878-7878-7878-787878787878");
    private readonly Guid _researchActivityId = Guid.Parse("79797979-7979-7979-7979-797979797979");
    private readonly Guid _milestoneLinkId = Guid.Parse("7a7a7a7a-7a7a-7a7a-7a7a-7a7a7a7a7a7a");
    private readonly Guid _moduleEnrollmentId = Guid.Parse("88888888-8888-8888-8888-888888888888");
    private readonly Guid _researchModuleEnrollmentId = Guid.Parse("89898989-8989-8989-8989-898989898989");
    private readonly Guid _progressId = Guid.Parse("99999999-9999-9999-9999-999999999999");

    private readonly InMemoryUnitOfWork _db = new();
    private readonly Mock<IClaimsService> _claimsService = new();
    private readonly Mock<IActivityProgressService> _activityProgressService = new();

    private EnrollmentCurriculumService CreateSut(Guid? currentUserId = null)
    {
        _claimsService.Setup(c => c.GetCurrentUserId).Returns(currentUserId ?? _studentId);

        _activityProgressService
            .Setup(s => s.CompleteActivityForModuleEnrollmentAsync(
                It.IsAny<Guid>(),
                It.IsAny<Guid>(),
                It.IsAny<Guid>(),
                It.IsAny<CompletionSource?>()))
            .Callback<Guid, Guid, Guid, CompletionSource?>((moduleEnrollmentId, activityId, studentId, _) =>
            {
                var existing = _db.ActivityProgresses.Items.FirstOrDefault(ap =>
                    ap.ModuleEnrollmentId == moduleEnrollmentId
                    && ap.ActivityId == activityId
                    && !ap.IsDeleted);

                if (existing != null)
                {
                    existing.ActivityStatus = ActivityStatus.Done;
                    existing.IsCompleted = true;
                    existing.CompletedAt = DateTime.UtcNow;
                    return;
                }

                _db.ActivityProgresses.Seed(new ActivityProgress
                {
                    Id = Guid.NewGuid(),
                    StudentId = studentId,
                    ActivityId = activityId,
                    ModuleEnrollmentId = moduleEnrollmentId,
                    ActivityStatus = ActivityStatus.Done,
                    IsCompleted = true,
                    CompletedAt = DateTime.UtcNow,
                    IsDeleted = false,
                });
            })
            .ReturnsAsync(new ActivityProgressResponseDto
            {
                ActivityId = _activity1Id,
                ModuleEnrollmentId = _moduleEnrollmentId,
                StudentId = _studentId,
                ActivityStatus = ActivityStatus.Done,
                IsCompleted = true,
                CompletedAt = DateTime.UtcNow,
            });

        _activityProgressService
            .Setup(s => s.SaveCheckpointForModuleEnrollmentAsync(
                It.IsAny<Guid>(),
                It.IsAny<Guid>(),
                It.IsAny<Guid>(),
                It.IsAny<string>()))
            .ReturnsAsync(new ActivityProgressResponseDto
            {
                ActivityId = _activity1Id,
                ModuleEnrollmentId = _moduleEnrollmentId,
                StudentId = _studentId,
                ActivityStatus = ActivityStatus.InProgress,
                ResumeState = new ActivityResumeStateDto { Kind = "video", PositionSeconds = 42 },
                LastAccessedAt = DateTime.UtcNow,
            });

        return new EnrollmentCurriculumService(
            _db,
            _claimsService.Object,
            _activityProgressService.Object,
            NullLogger<EnrollmentCurriculumService>.Instance);
    }

    private void SeedStudent(Guid? id = null)
    {
        _db.Users.Seed(new User
        {
            Id = id ?? _studentId,
            Code = "STD-001",
            Email = "student@test.com",
            Role = RoleType.Student,
            IsDeleted = false,
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
            IsDeleted = false,
        });
    }

    private void SeedProgramEnrollment(
        EnrollmentStatus status = EnrollmentStatus.Active,
        Guid? studentId = null,
        bool isDeleted = false,
        decimal progressPercent = 0m)
    {
        _db.ProgramEnrollments.Seed(new ProgramEnrollment
        {
            Id = _programEnrollmentId,
            StudentId = studentId ?? _studentId,
            ProgramId = _programId,
            Status = status,
            ProgressPercent = progressPercent,
            IsDeleted = isDeleted,
        });
    }

    private void SeedModuleEnrollment(
        Guid id,
        Guid moduleId,
        decimal progressPercent = 0m,
        EnrollmentStatus status = EnrollmentStatus.Active)
    {
        _db.ModuleEnrollments.Seed(new ModuleEnrollment
        {
            Id = id,
            StudentId = _studentId,
            ModuleId = moduleId,
            ProgramEnrollmentId = _programEnrollmentId,
            Status = status,
            ProgressPercent = progressPercent,
            AttemptNumber = 1,
            EnrolledAt = DateTime.UtcNow.AddDays(-5),
            IsDeleted = false,
        });
    }

    private void SeedCurriculum(bool researchUnlocked = false)
    {
        var theoryModule = new Module
        {
            Id = _theoryModuleId,
            Code = "MOD-THEORY",
            Name = "Theory Module",
            ProgramId = _programId,
            ModuleType = ModuleType.Theory,
            ModuleOrder = 1,
            Price = 100m,
            RetakeFee = 50m,
            IsMandatory = true,
            LearningOutcomes = ["Outcome A"],
            IsDeleted = false,
        };

        var researchModule = new Module
        {
            Id = _researchModuleId,
            Code = "MOD-RESEARCH",
            Name = "Research Module",
            ProgramId = _programId,
            ModuleType = ModuleType.Research,
            ModuleOrder = 2,
            PrerequisiteModuleId = _theoryModuleId,
            Price = 200m,
            RetakeFee = 75m,
            IsMandatory = true,
            LearningOutcomes = ["Outcome B"],
            IsDeleted = false,
        };

        var program = new Program
        {
            Id = _programId,
            Code = "PRG-001",
            Name = "STEAM Program",
            Category = ProgramCategory.Technology,
            Level = DifficultyLevel.Beginner,
            Status = "Published",
            Price = 1000m,
            IsDeleted = false,
            Modules = [theoryModule, researchModule],
        };

        _db.Programs.Seed(program);
        _db.Modules.Seed(theoryModule, researchModule);

        var course = new Course
        {
            Id = _courseId,
            Code = "CRS-001",
            Name = "Intro Course",
            ModuleId = _theoryModuleId,
            IsDeleted = false,
        };
        var researchCourseId = Guid.Parse("5a5a5a5a-5a5a-5a5a-5a5a-5a5a5a5a5a5a");
        var researchCourse = new Course
        {
            Id = researchCourseId,
            Code = "CRS-RSH",
            Name = "Research Course",
            ModuleId = _researchModuleId,
            IsDeleted = false,
        };
        _db.Courses.Seed(course, researchCourse);

        var activity1 = new Activity
        {
            Id = _activity1Id,
            Code = "ACT-001",
            Name = "Video Lesson",
            CourseId = _courseId,
            ActivityType = ActivityType.SelfPaced,
            ActivityOrder = 1,
            IsDeleted = false,
        };
        var activity2 = new Activity
        {
            Id = _activity2Id,
            Code = "ACT-002",
            Name = "Reading Lesson",
            CourseId = _courseId,
            ActivityType = ActivityType.SelfPaced,
            ActivityOrder = 2,
            IsDeleted = false,
        };
        var liveActivity = new Activity
        {
            Id = _liveActivityId,
            Code = "ACT-LIVE",
            Name = "Live Session",
            CourseId = _courseId,
            ActivityType = ActivityType.LiveOnline,
            ActivityOrder = 3,
            IsDeleted = false,
        };
        var researchActivity = new Activity
        {
            Id = _researchActivityId,
            Code = "ACT-RSH-001",
            Name = "Research Reading",
            CourseId = researchCourseId,
            ActivityType = ActivityType.SelfPaced,
            ActivityOrder = 1,
            IsDeleted = false,
        };

        _db.Activities.Seed(activity1, activity2, liveActivity, researchActivity);

        _db.Materials.Seed(new Material
        {
            Id = _materialId,
            ActivityId = _activity1Id,
            Title = "Intro Video",
            MaterialType = MaterialType.Video,
            IsDeleted = false,
        });

        _db.Assignments.Seed(
            new Assignment
            {
                Id = _moduleAssignmentId,
                Code = "ASG-MOD",
                Title = "Module Quiz",
                ModuleId = _theoryModuleId,
                AssignmentType = AssignmentType.Quiz,
                MaxPoints = 100,
                PassScore = 60m,
                IsRequiredForModulePass = true,
                IsDeleted = false,
            },
            new Assignment
            {
                Id = _courseAssignmentId,
                Code = "ASG-CRS",
                Title = "Course Upload",
                ModuleId = _theoryModuleId,
                CourseId = _courseId,
                AssignmentType = AssignmentType.FileUpload,
                MaxPoints = 100,
                PassScore = 70m,
                IsRequiredForModulePass = false,
                IsDeleted = false,
            },
            new Assignment
            {
                Id = _milestoneAssignmentId,
                Code = "ASG-RSH",
                Title = "Milestone Deliverable",
                ModuleId = _researchModuleId,
                AssignmentType = AssignmentType.FileUpload,
                MaxPoints = 100,
                PassScore = 70m,
                IsRequiredForModulePass = true,
                IsDeleted = false,
            });

        _db.ResearchMilestones.Seed(new ResearchMilestone
        {
            Id = _milestoneId,
            Code = "MLS-001",
            Title = "Proposal Milestone",
            ModuleId = _researchModuleId,
            MilestoneOrder = 1,
            IsCapstone = false,
            AssignmentId = _milestoneAssignmentId,
            IsDeleted = false,
        });

        _db.ResearchMilestoneActivities.Seed(new ResearchMilestoneActivity
        {
            Id = _milestoneLinkId,
            ResearchMilestoneId = _milestoneId,
            ActivityId = _researchActivityId,
            DisplayOrder = 1,
            IsRequiredForSubmission = true,
            IsDeleted = false,
        });

        if (researchUnlocked)
        {
            SeedModuleEnrollment(_moduleEnrollmentId, _theoryModuleId, progressPercent: 100m);
            SeedModuleEnrollment(_researchModuleEnrollmentId, _researchModuleId);
        }
    }

    private void SeedActivityProgress(
        Guid activityId,
        ActivityStatus status = ActivityStatus.InProgress,
        string? resumeStateJson = null,
        Guid? moduleEnrollmentId = null)
    {
        _db.ActivityProgresses.Seed(new ActivityProgress
        {
            Id = _progressId,
            StudentId = _studentId,
            ActivityId = activityId,
            ModuleEnrollmentId = moduleEnrollmentId ?? _moduleEnrollmentId,
            ActivityStatus = status,
            ResumeState = resumeStateJson,
            LastAccessedAt = DateTime.UtcNow.AddHours(-1),
            IsDeleted = false,
        });
    }

    // ── GetEnrollmentCurriculumAsync ──────────────────────────────────────────

    [Fact]
    public async Task GetCurriculum_ReturnsTheoryTree_ForStudent()
    {
        SeedStudent();
        SeedCurriculum();
        SeedProgramEnrollment();
        var sut = CreateSut();

        var result = await sut.GetEnrollmentCurriculumAsync(_programEnrollmentId);

        Assert.Equal(_programEnrollmentId, result.EnrollmentId);
        Assert.Equal(_programId, result.ProgramId);
        Assert.Equal("STEAM Program", result.ProgramName);
        Assert.Equal(2, result.Modules.Count);

        var theory = result.Modules[0];
        Assert.Equal(_theoryModuleId, theory.ModuleId);
        Assert.False(theory.IsLocked);
        Assert.Single(theory.Courses);
        Assert.Equal(3, theory.Courses[0].Activities.Count);
        Assert.NotNull(theory.Courses[0].Activities[0].Material);
        Assert.Equal(_activity1Id, result.CurrentActivityId);
    }

    [Fact]
    public async Task GetCurriculum_IncludesResearchMilestonePath_WhenUnlocked()
    {
        SeedStudent();
        SeedCurriculum(researchUnlocked: true);
        SeedProgramEnrollment();
        var sut = CreateSut();

        var result = await sut.GetEnrollmentCurriculumAsync(_programEnrollmentId);

        var research = result.Modules[1];
        Assert.Equal(ModuleType.Research, research.ModuleType);
        Assert.False(research.IsLocked);
        Assert.Single(research.Milestones);
        Assert.Equal("Proposal Milestone", research.Milestones[0].MilestoneName);
        Assert.Single(research.Milestones[0].Activities);
        Assert.NotNull(research.Milestones[0].Assignment);
    }

    [Fact]
    public async Task GetCurriculum_ProvisionsModuleEnrollment_WhenActiveUnlocked()
    {
        SeedStudent();
        SeedCurriculum();
        SeedProgramEnrollment();
        var sut = CreateSut();

        await sut.GetEnrollmentCurriculumAsync(_programEnrollmentId);

        Assert.Single(_db.ModuleEnrollments.Items);
        Assert.Equal(_theoryModuleId, _db.ModuleEnrollments.Items[0].ModuleId);
        Assert.Equal(EnrollmentStatus.Active, _db.ModuleEnrollments.Items[0].Status);
        Assert.Equal(1, _db.SaveChangesCallCount);
    }

    [Fact]
    public async Task GetCurriculum_AppliesResumeFields_WhenInProgress()
    {
        SeedStudent();
        SeedCurriculum();
        SeedProgramEnrollment();
        SeedModuleEnrollment(_moduleEnrollmentId, _theoryModuleId);
        SeedActivityProgress(
            _activity1Id,
            resumeStateJson: "{\"kind\":\"video\",\"positionSeconds\":90}");
        var sut = CreateSut();

        var result = await sut.GetEnrollmentCurriculumAsync(_programEnrollmentId);

        var activity = result.Modules[0].Courses[0].Activities[0];
        Assert.Equal("video", activity.ResumeState!.Kind);
        Assert.Equal(90, activity.ResumeState.PositionSeconds);
        Assert.NotNull(activity.LastAccessedAt);
    }

    [Fact]
    public async Task GetCurriculum_ReturnsDto_ForManager()
    {
        SeedStudent();
        SeedManager();
        SeedCurriculum();
        SeedProgramEnrollment();
        var sut = CreateSut(_managerId);

        var result = await sut.GetEnrollmentCurriculumAsync(_programEnrollmentId);

        Assert.Equal(_programEnrollmentId, result.EnrollmentId);
    }

    [Fact]
    public async Task GetCurriculum_ThrowsNotFound_WhenEnrollmentMissing()
    {
        SeedStudent();
        SeedCurriculum();
        var sut = CreateSut();

        await Assert.ThrowsAsync<NotFoundException>(() =>
            sut.GetEnrollmentCurriculumAsync(_programEnrollmentId));
    }

    [Fact]
    public async Task GetCurriculum_ThrowsForbidden_WhenOtherStudent()
    {
        SeedStudent();
        SeedStudent(_otherStudentId);
        SeedCurriculum();
        SeedProgramEnrollment(studentId: _otherStudentId);
        var sut = CreateSut();

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            sut.GetEnrollmentCurriculumAsync(_programEnrollmentId));
    }

    [Fact]
    public async Task GetCurriculum_ThrowsForbidden_WhenPendingPayment()
    {
        SeedStudent();
        SeedCurriculum();
        SeedProgramEnrollment(status: EnrollmentStatus.PendingPayment);
        var sut = CreateSut();

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            sut.GetEnrollmentCurriculumAsync(_programEnrollmentId));
    }

    [Fact]
    public async Task GetCurriculum_ThrowsUnauthorized_WhenNoUser()
    {
        SeedStudent();
        SeedCurriculum();
        SeedProgramEnrollment();
        var sut = CreateSut(Guid.Empty);

        await Assert.ThrowsAsync<UnauthorizedException>(() =>
            sut.GetEnrollmentCurriculumAsync(_programEnrollmentId));
    }

    // ── GetEnrollmentCurriculumMindMapAsync ─────────────────────────────────────

    [Fact]
    public async Task GetMindMap_ReturnsDto_ForStudent()
    {
        SeedStudent();
        SeedCurriculum();
        SeedProgramEnrollment();
        SeedModuleEnrollment(_moduleEnrollmentId, _theoryModuleId);
        var sut = CreateSut();

        var result = await sut.GetEnrollmentCurriculumMindMapAsync(_programEnrollmentId);

        Assert.Equal(_programEnrollmentId, result.EnrollmentId);
        Assert.Equal("STEAM Program", result.Hub.ProgramName);
        Assert.Equal(2, result.Modules.Count);
        Assert.NotEmpty(result.CurrentPaths);
    }

    [Fact]
    public async Task GetMindMap_ThrowsForbidden_WhenManager()
    {
        SeedStudent();
        SeedManager();
        SeedCurriculum();
        SeedProgramEnrollment();
        var sut = CreateSut(_managerId);

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            sut.GetEnrollmentCurriculumMindMapAsync(_programEnrollmentId));
    }

    // ── CompleteActivityAsync ───────────────────────────────────────────────────

    [Fact]
    public async Task CompleteActivity_ReturnsResponse_WhenSelfPacedAccessible()
    {
        SeedStudent();
        SeedCurriculum();
        SeedProgramEnrollment();
        SeedModuleEnrollment(_moduleEnrollmentId, _theoryModuleId);
        var sut = CreateSut();

        var result = await sut.CompleteActivityAsync(
            _programEnrollmentId,
            _activity1Id,
            new CompleteActivityRequestDto { Source = "video" });

        Assert.Equal("completed", result.ActivityStatus);
        Assert.Equal(_activity2Id, result.NextActivityId);
        _activityProgressService.Verify(
            s => s.CompleteActivityForModuleEnrollmentAsync(
                _moduleEnrollmentId,
                _activity1Id,
                _studentId,
                CompletionSource.Video),
            Times.Once);
    }

    [Fact]
    public async Task CompleteActivity_ThrowsNotFound_WhenActivityMissing()
    {
        SeedStudent();
        SeedCurriculum();
        SeedProgramEnrollment();
        SeedModuleEnrollment(_moduleEnrollmentId, _theoryModuleId);
        var sut = CreateSut();

        await Assert.ThrowsAsync<NotFoundException>(() =>
            sut.CompleteActivityAsync(
                _programEnrollmentId,
                Guid.Parse("00000000-0000-0000-0000-000000000001"),
                null));
    }

    [Fact]
    public async Task CompleteActivity_ThrowsBadRequest_WhenNotSelfPaced()
    {
        SeedStudent();
        SeedCurriculum();
        SeedProgramEnrollment();
        SeedModuleEnrollment(_moduleEnrollmentId, _theoryModuleId);
        var sut = CreateSut();

        await Assert.ThrowsAsync<BadRequestException>(() =>
            sut.CompleteActivityAsync(_programEnrollmentId, _liveActivityId, null));
    }

    [Fact]
    public async Task CompleteActivity_ThrowsForbidden_WhenLocked()
    {
        SeedStudent();
        SeedCurriculum();
        SeedProgramEnrollment();
        SeedModuleEnrollment(_moduleEnrollmentId, _theoryModuleId);
        var sut = CreateSut();

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            sut.CompleteActivityAsync(_programEnrollmentId, _activity2Id, null));
    }

    [Fact]
    public async Task CompleteActivity_ThrowsForbidden_WhenNotStudent()
    {
        SeedStudent();
        SeedManager();
        SeedCurriculum();
        SeedProgramEnrollment();
        SeedModuleEnrollment(_moduleEnrollmentId, _theoryModuleId);
        var sut = CreateSut(_managerId);

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            sut.CompleteActivityAsync(_programEnrollmentId, _activity1Id, null));
    }

    // ── EnsureActivityAccessibleAsync ───────────────────────────────────────────

    [Fact]
    public async Task EnsureAccessible_Completes_WhenAccessible()
    {
        SeedStudent();
        SeedCurriculum();
        SeedProgramEnrollment();
        SeedModuleEnrollment(_moduleEnrollmentId, _theoryModuleId);
        var sut = CreateSut();

        await sut.EnsureActivityAccessibleAsync(_programEnrollmentId, _activity1Id);
    }

    [Fact]
    public async Task EnsureAccessible_ThrowsForbidden_WhenLocked()
    {
        SeedStudent();
        SeedCurriculum();
        SeedProgramEnrollment();
        SeedModuleEnrollment(_moduleEnrollmentId, _theoryModuleId);
        var sut = CreateSut();

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            sut.EnsureActivityAccessibleAsync(_programEnrollmentId, _activity2Id));
    }

    [Fact]
    public async Task EnsureAccessible_ThrowsNotFound_WhenActivityMissing()
    {
        SeedStudent();
        SeedCurriculum();
        SeedProgramEnrollment();
        var sut = CreateSut();

        await Assert.ThrowsAsync<NotFoundException>(() =>
            sut.EnsureActivityAccessibleAsync(
                _programEnrollmentId,
                Guid.Parse("00000000-0000-0000-0000-000000000001")));
    }

    [Fact]
    public async Task EnsureAccessible_ThrowsNotFound_WhenEnrollmentMissing()
    {
        SeedStudent();
        SeedCurriculum();
        var sut = CreateSut();

        await Assert.ThrowsAsync<NotFoundException>(() =>
            sut.EnsureActivityAccessibleAsync(_programEnrollmentId, _activity1Id));
    }

    // ── EnsureStudentEnrolledInProgramAsync ─────────────────────────────────────

    [Fact]
    public async Task EnsureEnrolled_NoOp_WhenEmptyUserId()
    {
        SeedStudent();
        SeedCurriculum();
        SeedProgramEnrollment();
        var sut = CreateSut(Guid.Empty);

        await sut.EnsureStudentEnrolledInProgramAsync(_programId);
    }

    [Fact]
    public async Task EnsureEnrolled_NoOp_WhenNonStudent()
    {
        SeedManager();
        SeedCurriculum();
        SeedProgramEnrollment();
        var sut = CreateSut(_managerId);

        await sut.EnsureStudentEnrolledInProgramAsync(_programId);
    }

    [Fact]
    public async Task EnsureEnrolled_Completes_WhenActiveEnrollment()
    {
        SeedStudent();
        SeedCurriculum();
        SeedProgramEnrollment();
        var sut = CreateSut();

        await sut.EnsureStudentEnrolledInProgramAsync(_programId);
    }

    [Fact]
    public async Task EnsureEnrolled_ThrowsForbidden_WhenMissing()
    {
        SeedStudent();
        SeedCurriculum();
        var sut = CreateSut();

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            sut.EnsureStudentEnrolledInProgramAsync(_programId));
    }

    // ── SaveActivityCheckpointAsync ─────────────────────────────────────────────

    [Fact]
    public async Task SaveCheckpoint_ReturnsResponse_WhenValid()
    {
        SeedStudent();
        SeedCurriculum();
        SeedProgramEnrollment();
        SeedModuleEnrollment(_moduleEnrollmentId, _theoryModuleId);
        var sut = CreateSut();

        var result = await sut.SaveActivityCheckpointAsync(
            _programEnrollmentId,
            _activity1Id,
            new SaveActivityCheckpointRequestDto
            {
                ResumeState = new ActivityResumeStateDto
                {
                    Kind = "video",
                    PositionSeconds = 42,
                },
            });

        Assert.Equal(_activity1Id, result.ActivityId);
        Assert.Equal("InProgress", result.ActivityStatus);
        Assert.Equal("video", result.ResumeState!.Kind);
        Assert.NotNull(result.LastAccessedAt);
    }

    [Fact]
    public async Task SaveCheckpoint_ThrowsBadRequest_WhenInvalidResumeState()
    {
        SeedStudent();
        SeedCurriculum();
        SeedProgramEnrollment();
        SeedModuleEnrollment(_moduleEnrollmentId, _theoryModuleId);
        var sut = CreateSut();

        await Assert.ThrowsAsync<BadRequestException>(() =>
            sut.SaveActivityCheckpointAsync(
                _programEnrollmentId,
                _activity1Id,
                new SaveActivityCheckpointRequestDto
                {
                    ResumeState = new ActivityResumeStateDto { Kind = "invalid" },
                }));
    }

    [Fact]
    public async Task SaveCheckpoint_ThrowsForbidden_WhenLocked()
    {
        SeedStudent();
        SeedCurriculum();
        SeedProgramEnrollment();
        SeedModuleEnrollment(_moduleEnrollmentId, _theoryModuleId);
        var sut = CreateSut();

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            sut.SaveActivityCheckpointAsync(
                _programEnrollmentId,
                _activity2Id,
                new SaveActivityCheckpointRequestDto
                {
                    ResumeState = new ActivityResumeStateDto
                    {
                        Kind = "pdf",
                        Page = 1,
                    },
                }));
    }

    // ── GetActivityLearningProgressAsync ────────────────────────────────────────

    [Fact]
    public async Task GetLearningProgress_ReturnsDto_WhenProgressExists()
    {
        SeedStudent();
        SeedCurriculum();
        SeedProgramEnrollment();
        SeedModuleEnrollment(_moduleEnrollmentId, _theoryModuleId);
        SeedActivityProgress(
            _activity1Id,
            resumeStateJson: "{\"kind\":\"pdf\",\"page\":3}");
        var sut = CreateSut();

        var result = await sut.GetActivityLearningProgressAsync(_programEnrollmentId, _activity1Id);

        Assert.NotNull(result);
        Assert.Equal("InProgress", result!.ActivityStatus);
        Assert.Equal("pdf", result.ResumeState!.Kind);
        Assert.Equal(3, result.ResumeState.Page);
    }

    [Fact]
    public async Task GetLearningProgress_ReturnsNull_WhenNoProgress()
    {
        SeedStudent();
        SeedCurriculum();
        SeedProgramEnrollment();
        SeedModuleEnrollment(_moduleEnrollmentId, _theoryModuleId);
        var sut = CreateSut();

        var result = await sut.GetActivityLearningProgressAsync(_programEnrollmentId, _activity1Id);

        Assert.Null(result);
    }

    [Fact]
    public async Task GetLearningProgress_ThrowsForbidden_WhenLocked()
    {
        SeedStudent();
        SeedCurriculum();
        SeedProgramEnrollment();
        SeedModuleEnrollment(_moduleEnrollmentId, _theoryModuleId);
        var sut = CreateSut();

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            sut.GetActivityLearningProgressAsync(_programEnrollmentId, _activity2Id));
    }
}
