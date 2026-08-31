using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OboxSteam.Application.Commons;
using OboxSteam.Application.DTOs.QuizDTO;
using OboxSteam.Application.Exceptions;
using OboxSteam.Application.Interfaces;
using OboxSteam.Application.Notifications;
using OboxSteam.Application.Services;
using OboxSteam.Application.Validation;
using OboxSteam.Domain.Entities;
using OboxSteam.Domain.Enums;
using OboxSteam.Test.Helpers;

namespace OboxSteam.Test.UnitTests;

/// <summary>
/// After chuyen-ca, source-purchase submissions must not paint or mutate the new class
/// except rows copied onto the new module enrollments.
/// </summary>
public sealed class RebuySubmissionIsolationTests
{
    private readonly Guid _studentId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private readonly Guid _mentorId = Guid.Parse("14141414-1414-1414-1414-141414141414");
    private readonly Guid _managerId = Guid.Parse("13131313-1313-1313-1313-131313131313");
    private readonly Guid _parentId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private readonly Guid _programId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private readonly Guid _foundationsModuleId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private readonly Guid _labModuleId = Guid.Parse("34343434-3434-3434-3434-343434343434");
    private readonly Guid _foundationsCourseId = Guid.Parse("35353535-3535-3535-3535-353535353535");
    private readonly Guid _labCourseId = Guid.Parse("36363636-3636-3636-3636-363636363636");
    private readonly Guid _foundationsActivityId = Guid.Parse("37373737-3737-3737-3737-373737373737");
    private readonly Guid _labActivityId = Guid.Parse("38383838-3838-3838-3838-383838383838");
    private readonly Guid _foundationsQuizId = Guid.Parse("39393939-3939-3939-3939-393939393939");
    private readonly Guid _labQuizId = Guid.Parse("3a3a3a3a-3a3a-3a3a-3a3a-3a3a3a3a3a3a");
    private readonly Guid _sourcePeId = Guid.Parse("d2220423-bbc7-486d-a5f5-aa17ae0d6ba1");
    private readonly Guid _newPeId = Guid.Parse("af5c9f1b-3409-4f17-a312-73640862c9ae");
    private readonly Guid _sourceLabMeId = Guid.Parse("b1b1b1b1-b1b1-b1b1-b1b1-b1b1b1b1b1b1");
    private readonly Guid _sourceFoundationsMeId = Guid.Parse("b0b0b0b0-b0b0-b0b0-b0b0-b0b0b0b0b0b0");
    private readonly Guid _newLabMeId = Guid.Parse("6c804723-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private readonly Guid _newFoundationsMeId = Guid.Parse("c2c2c2c2-c2c2-c2c2-c2c2-c2c2c2c2c2c2");
    private readonly Guid _sourceFailSubmissionId = Guid.Parse("9bff112f-1111-1111-1111-111111111111");
    private readonly Guid _copiedFoundationsSubmissionId = Guid.Parse("d3d3d3d3-d3d3-d3d3-d3d3-d3d3d3d3d3d3");
    private readonly Guid _oldClassId = Guid.Parse("e4e4e4e4-e4e4-e4e4-e4e4-e4e4e4e4e4e4");
    private readonly Guid _newClassId = Guid.Parse("33095a67-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private readonly Guid _questionBankId = Guid.Parse("55555555-5555-5555-5555-555555555555");

    private readonly DateTime _now = new(2026, 8, 31, 10, 0, 0, DateTimeKind.Utc);
    private readonly InMemoryUnitOfWork _db = new();
    private readonly Mock<IClaimsService> _claimsService = new();
    private readonly Mock<IActivityProgressService> _activityProgressService = new();
    private readonly Mock<ICurrentTime> _currentTime = new();

    private void SetCurrentUser(Guid userId) =>
        _claimsService.Setup(c => c.GetCurrentUserId).Returns(userId);

    private void SeedLanRebuyFixture(bool includeCopiedFoundationsPass = true)
    {
        _db.Users.Seed(
            new User
            {
                Id = _studentId,
                Code = "STD-027",
                Email = "lan@test.com",
                FullName = "Lan Nguyen",
                Role = RoleType.Student,
                IsDeleted = false,
            },
            new User
            {
                Id = _mentorId,
                Code = "MNT-007",
                Email = "mentor7@test.com",
                FullName = "Mentor 7",
                Role = RoleType.Mentor,
                IsDeleted = false,
            },
            new User
            {
                Id = _managerId,
                Code = "MGR-001",
                Email = "manager@test.com",
                Role = RoleType.Manager,
                IsDeleted = false,
            });

        var foundations = new Module
        {
            Id = _foundationsModuleId,
            Code = "MOD-FOUND",
            Name = "Foundations",
            ProgramId = _programId,
            ModuleType = ModuleType.Theory,
            ModuleOrder = 1,
            IsDeleted = false,
        };
        var lab = new Module
        {
            Id = _labModuleId,
            Code = "MOD-FAILREBUY-01",
            Name = "Studio Lab",
            ProgramId = _programId,
            ModuleType = ModuleType.Experiential,
            ModuleOrder = 2,
            PrerequisiteModuleId = _foundationsModuleId,
            IsDeleted = false,
        };
        var program = new Program
        {
            Id = _programId,
            Code = "PRG-001",
            Name = "STEAM",
            Category = ProgramCategory.Technology,
            Level = DifficultyLevel.Beginner,
            Status = ProgramStatus.Active,
            IsDeleted = false,
            Modules = [foundations, lab],
        };
        _db.Programs.Seed(program);
        _db.Modules.Seed(foundations, lab);

        _db.Courses.Seed(
            new Course
            {
                Id = _foundationsCourseId,
                Code = "CRS-FOUND",
                Name = "Foundations Course",
                ModuleId = _foundationsModuleId,
                IsDeleted = false,
            },
            new Course
            {
                Id = _labCourseId,
                Code = "CRS-LAB",
                Name = "Studio Lab Course",
                ModuleId = _labModuleId,
                IsDeleted = false,
            });

        _db.Activities.Seed(
            new Activity
            {
                Id = _foundationsActivityId,
                Code = "ACT-FOUND",
                Name = "Foundations Lesson",
                CourseId = _foundationsCourseId,
                ActivityType = ActivityType.SelfPaced,
                ActivityOrder = 1,
                IsDeleted = false,
            },
            new Activity
            {
                Id = _labActivityId,
                Code = "ACT-LAB-1",
                Name = "Lab 1",
                CourseId = _labCourseId,
                ActivityType = ActivityType.SelfPaced,
                ActivityOrder = 1,
                IsDeleted = false,
            });

        _db.Assignments.Seed(
            new Assignment
            {
                Id = _foundationsQuizId,
                Code = "ASG-FOUND-Q",
                Title = "Foundations Quiz",
                ModuleId = _foundationsModuleId,
                CourseId = _foundationsCourseId,
                AssignmentType = AssignmentType.Quiz,
                QuestionBankId = _questionBankId,
                MaxPoints = 100,
                PassScore = 50m,
                IsRequiredForModulePass = true,
                IsDeleted = false,
            },
            new Assignment
            {
                Id = _labQuizId,
                Code = "ASG-LAB-Q",
                Title = "Studio Lab Quiz",
                ModuleId = _labModuleId,
                CourseId = _labCourseId,
                AssignmentType = AssignmentType.Quiz,
                QuestionBankId = _questionBankId,
                MaxPoints = 100,
                PassScore = 50m,
                IsRequiredForModulePass = true,
                IsDeleted = false,
            });

        _db.ProgramEnrollments.Seed(
            new ProgramEnrollment
            {
                Id = _sourcePeId,
                StudentId = _studentId,
                ProgramId = _programId,
                Status = EnrollmentStatus.Failed,
                EndReason = ProgramPurchaseEndReason.AcademicFail,
                EndedModuleId = _labModuleId,
                EndedAt = new DateTime(2026, 8, 30, 0, 0, 0, DateTimeKind.Utc),
                ProgressPercent = 38.46m,
                IsDeleted = false,
            },
            new ProgramEnrollment
            {
                Id = _newPeId,
                StudentId = _studentId,
                ProgramId = _programId,
                Status = EnrollmentStatus.Active,
                SourceProgramEnrollmentId = _sourcePeId,
                ProgressPercent = 38.46m,
                IsDeleted = false,
            });

        _db.ModuleEnrollments.Seed(
            new ModuleEnrollment
            {
                Id = _sourceFoundationsMeId,
                StudentId = _studentId,
                ModuleId = _foundationsModuleId,
                ProgramEnrollmentId = _sourcePeId,
                Status = EnrollmentStatus.Completed,
                AttemptNumber = 1,
                ProgressPercent = 100m,
                IsDeleted = false,
            },
            new ModuleEnrollment
            {
                Id = _sourceLabMeId,
                StudentId = _studentId,
                ModuleId = _labModuleId,
                ProgramEnrollmentId = _sourcePeId,
                Status = EnrollmentStatus.Failed,
                AttemptNumber = 1,
                IsDeleted = false,
            },
            new ModuleEnrollment
            {
                Id = _newLabMeId,
                StudentId = _studentId,
                ModuleId = _labModuleId,
                ProgramEnrollmentId = _newPeId,
                Status = EnrollmentStatus.Active,
                AttemptNumber = 2,
                ProgressPercent = 0m,
                IsDeleted = false,
            },
            new ModuleEnrollment
            {
                Id = _newFoundationsMeId,
                StudentId = _studentId,
                ModuleId = _foundationsModuleId,
                ProgramEnrollmentId = _newPeId,
                Status = EnrollmentStatus.Completed,
                AttemptNumber = 2,
                ProgressPercent = 100m,
                IsDeleted = false,
            });

        _db.ActivityProgresses.Seed(
            new ActivityProgress
            {
                Id = Guid.NewGuid(),
                StudentId = _studentId,
                ActivityId = _foundationsActivityId,
                ModuleEnrollmentId = _newFoundationsMeId,
                ActivityStatus = ActivityStatus.Done,
                IsCompleted = true,
                CompletedAt = _now.AddDays(-3),
                IsDeleted = false,
            },
            new ActivityProgress
            {
                Id = Guid.NewGuid(),
                StudentId = _studentId,
                ActivityId = _labActivityId,
                ModuleEnrollmentId = _newLabMeId,
                ActivityStatus = ActivityStatus.Done,
                IsCompleted = true,
                CompletedAt = _now.AddDays(-1),
                IsDeleted = false,
            });

        _db.Submissions.Seed(new Submission
        {
            Id = _sourceFailSubmissionId,
            Code = "SUB-OLD-FAIL",
            AssignmentId = _labQuizId,
            StudentId = _studentId,
            ModuleEnrollmentId = _sourceLabMeId,
            AttemptNumber = 1,
            Status = SubmissionStatus.Graded,
            AssignedGrade = 10m,
            SubmittedAt = new DateTime(2026, 8, 27, 0, 0, 0, DateTimeKind.Utc),
            GradedAt = new DateTime(2026, 8, 28, 0, 0, 0, DateTimeKind.Utc),
            IsDeleted = false,
        });

        if (includeCopiedFoundationsPass)
        {
            _db.Submissions.Seed(new Submission
            {
                Id = _copiedFoundationsSubmissionId,
                Code = "SUB-COPY-FOUND",
                AssignmentId = _foundationsQuizId,
                StudentId = _studentId,
                ModuleEnrollmentId = _newFoundationsMeId,
                AttemptNumber = 1,
                Status = SubmissionStatus.Graded,
                AssignedGrade = 80m,
                SubmittedAt = new DateTime(2026, 8, 20, 0, 0, 0, DateTimeKind.Utc),
                GradedAt = new DateTime(2026, 8, 20, 0, 0, 0, DateTimeKind.Utc),
                IsDeleted = false,
            });
        }

        _db.Classes.Seed(
            new Class
            {
                Id = _oldClassId,
                Code = "CLS-CURRENT",
                Name = "Old cohort",
                ProgramId = _programId,
                MentorId = _mentorId,
                Status = ClassStatus.InProgress,
                Kind = ClassKind.Standard,
                StartDate = _now.AddDays(-60),
                EndDate = _now.AddDays(30),
                MaxCapacity = 30,
                IsDeleted = false,
            },
            new Class
            {
                Id = _newClassId,
                Code = "CLS-FAILREBUY-ELIGIBLE",
                Name = "Eligible cohort",
                ProgramId = _programId,
                MentorId = _mentorId,
                Status = ClassStatus.InProgress,
                Kind = ClassKind.Standard,
                StartDate = _now.AddDays(-14),
                EndDate = _now.AddDays(90),
                MaxCapacity = 30,
                IsDeleted = false,
            });

        _db.ClassEnrollments.Seed(
            new ClassEnrollment
            {
                Id = Guid.NewGuid(),
                ClassId = _oldClassId,
                StudentId = _studentId,
                ProgramEnrollmentId = _sourcePeId,
                Status = ClassEnrollmentStatus.Withdrawn,
                IsDeleted = false,
            },
            new ClassEnrollment
            {
                Id = Guid.NewGuid(),
                ClassId = _newClassId,
                StudentId = _studentId,
                ProgramEnrollmentId = _newPeId,
                Status = ClassEnrollmentStatus.Active,
                IsDeleted = false,
            });

        ClassAssignmentWindowSeed.Open(
            _db,
            _newClassId,
            _labModuleId,
            _labQuizId,
            start: DateTime.UtcNow.AddDays(14),
            end: DateTime.UtcNow.AddDays(60));
        ClassAssignmentWindowSeed.Open(
            _db,
            _newClassId,
            _foundationsModuleId,
            _foundationsQuizId,
            start: _now.AddDays(-30),
            end: _now.AddDays(30));
    }

    private EnrollmentCurriculumService CreateCurriculumSut()
    {
        SetCurrentUser(_studentId);
        return new EnrollmentCurriculumService(
            _db,
            _claimsService.Object,
            _activityProgressService.Object,
            NullLogger<EnrollmentCurriculumService>.Instance);
    }

    [Fact]
    public async Task Curriculum_ActiveRebuy_DoesNotShowSourceLabFail_AndKeepsCopiedFoundations()
    {
        SeedLanRebuyFixture();
        var sut = CreateCurriculumSut();

        var result = await sut.GetEnrollmentCurriculumAsync(_newPeId);
        var lab = result.Modules.Single(m => m.ModuleId == _labModuleId);
        var labQuiz = lab.Courses[0].Assignments.Single(a => a.AssignmentId == _labQuizId);
        var foundations = result.Modules.Single(m => m.ModuleId == _foundationsModuleId);
        var foundationsQuiz = foundations.Courses[0].Assignments.Single(a => a.AssignmentId == _foundationsQuizId);

        Assert.NotEqual(CurriculumStatusHelper.StatusSubmitted, labQuiz.Status);
        Assert.Equal(CurriculumStatusHelper.StatusLocked, labQuiz.Status);
        Assert.NotNull(labQuiz.AvailableFrom);
        Assert.Equal(CurriculumStatusHelper.StatusCompleted, foundationsQuiz.Status);
    }

    [Fact]
    public async Task Curriculum_FailedSource_KeepsLabFail_AndDoesNotFlipAfterNewPass()
    {
        SeedLanRebuyFixture();
        _db.Submissions.Seed(new Submission
        {
            Id = Guid.NewGuid(),
            Code = "SUB-NEW-PASS",
            AssignmentId = _labQuizId,
            StudentId = _studentId,
            ModuleEnrollmentId = _newLabMeId,
            AttemptNumber = 1,
            Status = SubmissionStatus.Graded,
            AssignedGrade = 90m,
            IsDeleted = false,
        });
        var sut = CreateCurriculumSut();

        var failedPe = await sut.GetEnrollmentCurriculumAsync(_sourcePeId);
        var failedLabQuiz = failedPe.Modules
            .Single(m => m.ModuleId == _labModuleId)
            .Courses[0].Assignments.Single(a => a.AssignmentId == _labQuizId);

        Assert.Equal(CurriculumStatusHelper.StatusSubmitted, failedLabQuiz.Status);

        var activePe = await sut.GetEnrollmentCurriculumAsync(_newPeId);
        var activeLabQuiz = activePe.Modules
            .Single(m => m.ModuleId == _labModuleId)
            .Courses[0].Assignments.Single(a => a.AssignmentId == _labQuizId);
        Assert.Equal(CurriculumStatusHelper.StatusCompleted, activeLabQuiz.Status);
    }

    [Fact]
    public async Task ParentProgression_ActiveRebuy_DoesNotShowSourceLabFail()
    {
        SeedLanRebuyFixture();
        _db.Users.Seed(new User
        {
            Id = _parentId,
            Code = "PAR-001",
            Email = "parent@test.com",
            Role = RoleType.Parent,
            IsDeleted = false,
        });
        _db.ParentStudents.Seed(new ParentStudent
        {
            Id = Guid.NewGuid(),
            ParentId = _parentId,
            StudentId = _studentId,
            IsVerified = true,
            IsDeleted = false,
        });
        SetCurrentUser(_parentId);
        var sut = new ParentProgressionService(
            _db,
            _claimsService.Object,
            NullLogger<ParentProgressionService>.Instance);

        var result = await sut.GetEnrollmentProgressionAsync(_studentId, _newPeId);
        var labQuiz = result.Modules
            .Single(m => m.ModuleId == _labModuleId)
            .Assignments.Single(a => a.AssignmentId == _labQuizId);

        Assert.NotEqual(CurriculumStatusHelper.StatusSubmitted, labQuiz.Status);
        Assert.NotEqual(CurriculumStatusHelper.StatusCompleted, labQuiz.Status);
    }

    [Fact]
    public async Task MentorList_NewClass_OmitsSourceFail_AndIncludesCopiedFoundations()
    {
        SeedLanRebuyFixture();
        SetCurrentUser(_managerId);
        var sut = new AssignmentService(
            _claimsService.Object,
            _db,
            NullLogger<AssignmentService>.Instance,
            Mock.Of<INotificationPublisher>(),
            new FakeSyncEventPublisher());

        var labRows = await sut.GetAssignmentSubmissions(_labQuizId, _newClassId);
        var foundationsRows = await sut.GetAssignmentSubmissions(_foundationsQuizId, _newClassId);

        Assert.DoesNotContain(labRows, r => r.SubmissionId == _sourceFailSubmissionId);
        Assert.Contains(foundationsRows, r => r.SubmissionId == _copiedFoundationsSubmissionId);
    }

    [Fact]
    public async Task ClassCurriculumProgress_IgnoresSourceLabFail()
    {
        SeedLanRebuyFixture();
        SetCurrentUser(_mentorId);
        var sut = new ClassCurriculumProgressService(
            _db,
            _claimsService.Object,
            NullLogger<ClassCurriculumProgressService>.Instance);

        var result = await sut.GetCurriculumProgressAsync(_newClassId);
        var lab = result.Modules.Single(m => m.ModuleId == _labModuleId);
        var labQuiz = lab.Assignments.Single(a => a.AssignmentId == _labQuizId);
        var foundations = result.Modules.Single(m => m.ModuleId == _foundationsModuleId);
        var foundationsQuiz = foundations.Assignments.Single(a => a.AssignmentId == _foundationsQuizId);

        Assert.Equal(0, labQuiz.SubmittedCount);
        Assert.Equal(0, labQuiz.GradedCount);
        Assert.Equal(1, foundationsQuiz.SubmittedCount);
        Assert.Equal(1, foundationsQuiz.GradedCount);
    }

    [Fact]
    public async Task QuizSet_NewClass_IsNotLocked_BySourceSubmission()
    {
        SeedLanRebuyFixture();
        _db.ClassQuizQuestionSets.Seed(new ClassQuizQuestionSet
        {
            Id = Guid.NewGuid(),
            ClassId = _newClassId,
            AssignmentId = _labQuizId,
            IsDeleted = false,
        });
        SetCurrentUser(_mentorId);
        var sut = new ClassQuizQuestionSetService(
            _db,
            _claimsService.Object,
            Mock.Of<INotificationPublisher>(n =>
                n.PublishAsync(It.IsAny<NotificationCommand>(), It.IsAny<CancellationToken>())
                == Task.CompletedTask),
            NullLogger<ClassQuizQuestionSetService>.Instance);

        var result = await sut.GetAsync(_labQuizId, _newClassId);

        Assert.False(result.IsLocked);
    }

    [Fact]
    public async Task SaveAndSubmitQuiz_OnSourcePending_ThrowsForbidden_AfterRebuy()
    {
        SeedLanRebuyFixture();
        var pendingId = Guid.NewGuid();
        var questionId = Guid.NewGuid();
        var optionId = Guid.NewGuid();
        _db.Submissions.Seed(new Submission
        {
            Id = pendingId,
            Code = "SUB-OLD-PEND",
            AssignmentId = _labQuizId,
            StudentId = _studentId,
            ModuleEnrollmentId = _sourceLabMeId,
            Status = SubmissionStatus.Pending,
            AttemptNumber = 1,
            IsDeleted = false,
        });
        _db.QuizQuestions.Seed(new QuizQuestion
        {
            Id = questionId,
            AssignmentId = _labQuizId,
            SubmissionId = pendingId,
            QuestionText = "Q",
            QuestionType = QuestionTypeConstants.SingleChoice,
            Points = 1,
            OrderIndex = 1,
            IsDeleted = false,
        });
        _db.QuizOptions.Seed(new QuizOption
        {
            Id = optionId,
            QuestionId = questionId,
            OptionText = "A",
            IsCorrect = true,
            IsDeleted = false,
        });
        SetCurrentUser(_studentId);
        _currentTime.Setup(t => t.GetCurrentTime()).Returns(_now);
        var lifecycle = new ProgramPurchaseLifecycle(
            _db,
            _currentTime.Object,
            Mock.Of<INotificationPublisher>(),
            NullLogger<ProgramPurchaseLifecycle>.Instance);
        var sut = new QuizAttemptService(
            _claimsService.Object,
            _db,
            Mock.Of<ICertificateService>(),
            Mock.Of<INotificationPublisher>(n =>
                n.PublishAsync(It.IsAny<NotificationCommand>(), It.IsAny<CancellationToken>())
                == Task.CompletedTask),
            NullLogger<QuizAttemptService>.Instance,
            lifecycle);

        var draft = new SaveDraftAnswersRequestDto
        {
            Answers = [new QuizAnswerItemDto { QuestionId = questionId, SelectedOptionIds = [optionId] }],
        };
        var submit = new SubmitQuizAnswersRequestDto
        {
            Answers = [new QuizAnswerItemDto { QuestionId = questionId, SelectedOptionIds = [optionId] }],
        };

        var saveEx = await Assert.ThrowsAsync<ForbiddenException>(() => sut.SaveDraftAnswers(pendingId, draft));
        var submitEx = await Assert.ThrowsAsync<ForbiddenException>(() => sut.SubmitQuiz(pendingId, submit));
        Assert.Equal(QuizAttemptValidator.EnrollmentNotActiveMessage, saveEx.Message);
        Assert.Equal(QuizAttemptValidator.EnrollmentNotActiveMessage, submitEx.Message);
    }

    [Fact]
    public async Task StartQuiz_OnNewLabMe_HasZeroCompletedAttempts()
    {
        SeedLanRebuyFixture();
        var completed = await AssessmentAttemptPolicy.CountCompletedAttemptsAsync(
            _db,
            _labQuizId,
            _studentId,
            _newLabMeId);

        Assert.Equal(0, completed);
    }

    [Fact]
    public async Task TryExtend_OnSourceMe_DoesNotPadNewClassWindow()
    {
        SeedLanRebuyFixture();
        var nextAssignmentId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
        _db.Assignments.Seed(new Assignment
        {
            Id = nextAssignmentId,
            Code = "ASG-MS2",
            Title = "Milestone 2",
            ModuleId = _labModuleId,
            AssignmentType = AssignmentType.FileUpload,
            MaxPoints = 100,
            PassScore = 50,
            IsDeleted = false,
        });
        _db.ResearchMilestones.Seed(
            new ResearchMilestone
            {
                Id = Guid.Parse("cccccccc-cccc-cccc-cccc-ccccccccccc1"),
                Code = "MS-1",
                Title = "M1",
                ModuleId = _labModuleId,
                AssignmentId = _labQuizId,
                MilestoneOrder = 1,
                IsDeleted = false,
            },
            new ResearchMilestone
            {
                Id = Guid.Parse("cccccccc-cccc-cccc-cccc-ccccccccccc2"),
                Code = "MS-2",
                Title = "M2",
                ModuleId = _labModuleId,
                AssignmentId = nextAssignmentId,
                MilestoneOrder = 2,
                IsDeleted = false,
            });
        var newWindow = ClassAssignmentWindowSeed.Open(
            _db,
            _newClassId,
            _labModuleId,
            nextAssignmentId,
            start: _now.AddDays(-10),
            end: _now.AddHours(-1));
        var originalEnd = newWindow.EndTime;
        _currentTime.Setup(t => t.GetCurrentTime()).Returns(_now);
        var sut = new ProgramPurchaseLifecycle(
            _db,
            _currentTime.Object,
            Mock.Of<INotificationPublisher>(),
            NullLogger<ProgramPurchaseLifecycle>.Instance);

        await sut.TryExtendNextMilestoneWindowAfterPassAsync(
            _db.Assignments.Items.Single(a => a.Id == _labQuizId),
            _studentId,
            _sourceLabMeId);

        Assert.Equal(originalEnd, newWindow.EndTime);
    }
}
