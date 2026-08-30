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

public sealed class ProgramPurchaseLifecycleTests
{
    private readonly Guid _studentId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private readonly Guid _programId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private readonly Guid _moduleId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private readonly Guid _enrollmentId = Guid.Parse("66666666-6666-6666-6666-666666666666");
    private readonly Guid _classId = Guid.Parse("99999999-9999-9999-9999-999999999999");

    private readonly DateTime _now = new(2026, 8, 29, 10, 0, 0, DateTimeKind.Utc);

    private readonly InMemoryUnitOfWork _db = new();
    private readonly Mock<ICurrentTime> _currentTime = new();
    private readonly Mock<INotificationPublisher> _notificationPublisher = new();

    private ProgramPurchaseLifecycle CreateSut()
    {
        _currentTime.Setup(t => t.GetCurrentTime()).Returns(_now);
        _notificationPublisher
            .Setup(n => n.PublishAsync(It.IsAny<NotificationCommand>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        return new ProgramPurchaseLifecycle(
            _db,
            _currentTime.Object,
            _notificationPublisher.Object,
            NullLogger<ProgramPurchaseLifecycle>.Instance);
    }

    private ProgramEnrollment SeedEnrollment(EnrollmentStatus status = EnrollmentStatus.Active)
    {
        var enrollment = new ProgramEnrollment
        {
            Id = _enrollmentId,
            StudentId = _studentId,
            ProgramId = _programId,
            Status = status,
            IsDeleted = false,
        };
        _db.ProgramEnrollments.Seed(enrollment);
        return enrollment;
    }

    private void SeedSeats(params ClassEnrollmentStatus[] statuses)
    {
        foreach (var status in statuses)
        {
            _db.ClassEnrollments.Seed(new ClassEnrollment
            {
                Id = Guid.NewGuid(),
                ClassId = Guid.NewGuid(),
                StudentId = _studentId,
                ProgramEnrollmentId = _enrollmentId,
                Status = status,
                IsDeleted = false,
            });
        }
    }

    [Fact]
    public async Task CloseAsync_Attendance_MapsToFailed_AndWithdrawsSeats()
    {
        var enrollment = SeedEnrollment();
        SeedSeats(ClassEnrollmentStatus.Active, ClassEnrollmentStatus.Pending);
        var sut = CreateSut();

        await sut.CloseAsync(enrollment, ProgramPurchaseEndReason.Attendance, _moduleId);

        Assert.Equal(EnrollmentStatus.Failed, enrollment.Status);
        Assert.Equal(ProgramPurchaseEndReason.Attendance, enrollment.EndReason);
        Assert.Equal(_moduleId, enrollment.EndedModuleId);
        Assert.Equal(_now, enrollment.EndedAt);

        Assert.All(
            _db.ClassEnrollments.Items,
            seat => Assert.Equal(ClassEnrollmentStatus.Withdrawn, seat.Status));

        _notificationPublisher.Verify(
            n => n.PublishAsync(
                It.Is<NotificationCommand>(c =>
                    c.Type == NotificationType.ModuleFailed
                    && c.Body != null
                    && c.Body.Contains("vắng từ 50% số buổi")),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task CloseAsync_Withdraw_MapsToDropped()
    {
        var enrollment = SeedEnrollment();
        var sut = CreateSut();

        await sut.CloseAsync(enrollment, ProgramPurchaseEndReason.Withdraw, endedModuleId: null);

        Assert.Equal(EnrollmentStatus.Dropped, enrollment.Status);
        Assert.Equal(ProgramPurchaseEndReason.Withdraw, enrollment.EndReason);
        Assert.Null(enrollment.EndedModuleId);
        Assert.Equal(_now, enrollment.EndedAt);

        _notificationPublisher.Verify(
            n => n.PublishAsync(
                It.Is<NotificationCommand>(c => c.Type == NotificationType.ProgramWithdrawn),
                It.IsAny<CancellationToken>()),
            Times.Once);
        _notificationPublisher.Verify(
            n => n.PublishAsync(
                It.Is<NotificationCommand>(c => c.Type == NotificationType.ModuleFailed),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task CloseAsync_AcademicFail_FailsEndedModule_AndDropsOtherOpenModules()
    {
        var enrollment = SeedEnrollment();
        var otherModuleId = Guid.Parse("44444444-4444-4444-4444-444444444444");
        _db.Modules.Seed(new Module
        {
            Id = _moduleId,
            ProgramId = _programId,
            Code = "MOD-END",
            Name = "Lab",
            IsDeleted = false,
        });
        var endedMe = new ModuleEnrollment
        {
            Id = Guid.NewGuid(),
            StudentId = _studentId,
            ModuleId = _moduleId,
            ProgramEnrollmentId = _enrollmentId,
            Status = EnrollmentStatus.Active,
            AttemptNumber = 1,
            IsDeleted = false,
        };
        var otherMe = new ModuleEnrollment
        {
            Id = Guid.NewGuid(),
            StudentId = _studentId,
            ModuleId = otherModuleId,
            ProgramEnrollmentId = _enrollmentId,
            Status = EnrollmentStatus.Active,
            AttemptNumber = 1,
            IsDeleted = false,
        };
        var completedMe = new ModuleEnrollment
        {
            Id = Guid.NewGuid(),
            StudentId = _studentId,
            ModuleId = Guid.NewGuid(),
            ProgramEnrollmentId = _enrollmentId,
            Status = EnrollmentStatus.Completed,
            AttemptNumber = 1,
            IsDeleted = false,
        };
        _db.ModuleEnrollments.Seed(endedMe);
        _db.ModuleEnrollments.Seed(otherMe);
        _db.ModuleEnrollments.Seed(completedMe);
        var sut = CreateSut();

        await sut.CloseAsync(enrollment, ProgramPurchaseEndReason.AcademicFail, _moduleId);

        Assert.Equal(EnrollmentStatus.Failed, endedMe.Status);
        Assert.Equal(EnrollmentStatus.Dropped, otherMe.Status);
        Assert.Equal(EnrollmentStatus.Completed, completedMe.Status);
        _notificationPublisher.Verify(
            n => n.PublishAsync(
                It.Is<NotificationCommand>(c =>
                    c.Type == NotificationType.ModuleFailed
                    && c.Body != null
                    && c.Body.Contains("chuyển ca")
                    && !c.Body.Contains("vắng từ 50% số buổi")),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task CloseAsync_Withdraw_DropsOpenModules_KeepsCompleted()
    {
        var enrollment = SeedEnrollment();
        var openMe = new ModuleEnrollment
        {
            Id = Guid.NewGuid(),
            StudentId = _studentId,
            ModuleId = _moduleId,
            ProgramEnrollmentId = _enrollmentId,
            Status = EnrollmentStatus.Active,
            AttemptNumber = 1,
            IsDeleted = false,
        };
        var completedMe = new ModuleEnrollment
        {
            Id = Guid.NewGuid(),
            StudentId = _studentId,
            ModuleId = Guid.NewGuid(),
            ProgramEnrollmentId = _enrollmentId,
            Status = EnrollmentStatus.Completed,
            AttemptNumber = 1,
            IsDeleted = false,
        };
        _db.ModuleEnrollments.Seed(openMe);
        _db.ModuleEnrollments.Seed(completedMe);
        var sut = CreateSut();

        await sut.CloseAsync(enrollment, ProgramPurchaseEndReason.Withdraw, endedModuleId: null);

        Assert.Equal(EnrollmentStatus.Dropped, openMe.Status);
        Assert.Equal(EnrollmentStatus.Completed, completedMe.Status);
        Assert.Equal(EnrollmentStatus.Dropped, enrollment.Status);
    }

    [Fact]
    public async Task CloseAsync_AlreadyFailed_IsNoOp()
    {
        var enrollment = SeedEnrollment(EnrollmentStatus.Failed);
        var originalEndedAt = enrollment.EndedAt;
        var sut = CreateSut();

        await sut.CloseAsync(enrollment, ProgramPurchaseEndReason.Attendance, _moduleId);

        Assert.Equal(EnrollmentStatus.Failed, enrollment.Status);
        Assert.Equal(originalEndedAt, enrollment.EndedAt);
        _notificationPublisher.Verify(
            n => n.PublishAsync(It.IsAny<NotificationCommand>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task CloseAsync_AlreadyDropped_IsNoOp()
    {
        var enrollment = SeedEnrollment(EnrollmentStatus.Dropped);
        var sut = CreateSut();

        await sut.CloseAsync(enrollment, ProgramPurchaseEndReason.Withdraw);

        Assert.Equal(EnrollmentStatus.Dropped, enrollment.Status);
        _notificationPublisher.Verify(
            n => n.PublishAsync(It.IsAny<NotificationCommand>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task CloseAsync_NoSeats_StillClosesEnrollment()
    {
        var enrollment = SeedEnrollment();
        var sut = CreateSut();

        await sut.CloseAsync(enrollment, ProgramPurchaseEndReason.AcademicFail, _moduleId);

        Assert.Equal(EnrollmentStatus.Failed, enrollment.Status);
        Assert.Equal(ProgramPurchaseEndReason.AcademicFail, enrollment.EndReason);
        Assert.Equal(_moduleId, enrollment.EndedModuleId);
    }

    // ── TryCloseAfterFailedAssignmentAsync ───────────────────────────────────

    private readonly Guid _assignmentId = Guid.Parse("77777777-7777-7777-7777-777777777777");
    private readonly Guid _moduleEnrollmentId = Guid.Parse("88888888-8888-8888-8888-888888888888");

    private void SeedAcademicContext(
        int maxAttempts = 1,
        ModuleType moduleType = ModuleType.Experiential,
        EnrollmentStatus programStatus = EnrollmentStatus.Active,
        bool isRequiredForModulePass = true)
    {
        _db.Users.Seed(new User
        {
            Id = _studentId,
            Code = "STD-001",
            Email = "student@test.com",
            Role = RoleType.Student,
            IsDeleted = false,
        });
        _db.Programs.Seed(new Program
        {
            Id = _programId,
            Code = "PRG-001",
            Name = "Program",
            IsDeleted = false,
        });
        _db.Modules.Seed(new Module
        {
            Id = _moduleId,
            Code = "MOD-001",
            Name = "Module",
            ProgramId = _programId,
            ModuleType = moduleType,
            IsDeleted = false,
        });
        _db.Assignments.Seed(new Assignment
        {
            Id = _assignmentId,
            Code = "ASM-001",
            Title = "Assignment",
            ModuleId = _moduleId,
            AssignmentType = AssignmentType.FileUpload,
            MaxPoints = 100,
            PassScore = 50,
            MaxAttempts = maxAttempts,
            IsRequiredForModulePass = isRequiredForModulePass,
            IsDeleted = false,
        });
        _db.ProgramEnrollments.Seed(new ProgramEnrollment
        {
            Id = _enrollmentId,
            StudentId = _studentId,
            ProgramId = _programId,
            Status = programStatus,
            IsDeleted = false,
        });
        _db.ModuleEnrollments.Seed(new ModuleEnrollment
        {
            Id = _moduleEnrollmentId,
            StudentId = _studentId,
            ModuleId = _moduleId,
            ProgramEnrollmentId = _enrollmentId,
            Status = EnrollmentStatus.Active,
            AttemptNumber = 1,
            IsDeleted = false,
        });
    }

    private void SeedFailedSubmission(int attemptNumber)
    {
        _db.Submissions.Seed(new Submission
        {
            Id = Guid.NewGuid(),
            Code = $"SUB-{attemptNumber}",
            AssignmentId = _assignmentId,
            StudentId = _studentId,
            ModuleEnrollmentId = _moduleEnrollmentId,
            AttemptNumber = attemptNumber,
            Status = SubmissionStatus.Graded,
            AssignedGrade = 10,
            IsDeleted = false,
        });
    }

    private void SeedRecoveryRequest(AssessmentRecoveryRequestStatus status)
    {
        _db.AssessmentRecoveryRequests.Seed(new AssessmentRecoveryRequest
        {
            Id = Guid.NewGuid(),
            StudentId = _studentId,
            AssignmentId = _assignmentId,
            ModuleEnrollmentId = _moduleEnrollmentId,
            Status = status,
            IsDeleted = false,
        });
    }

    [Fact]
    public async Task TryCloseAfterFailedAssignmentAsync_Closes_WhenAllConditionsMet()
    {
        SeedAcademicContext(maxAttempts: 1);
        SeedFailedSubmission(attemptNumber: 1);
        SeedRecoveryRequest(AssessmentRecoveryRequestStatus.Approved);
        SeedRecoveryRequest(AssessmentRecoveryRequestStatus.Rejected);
        var sut = CreateSut();

        await sut.TryCloseAfterFailedAssignmentAsync(_studentId, _assignmentId, _moduleEnrollmentId);

        var enrollment = _db.ProgramEnrollments.Items.Single(pe => pe.Id == _enrollmentId);
        Assert.Equal(EnrollmentStatus.Failed, enrollment.Status);
        Assert.Equal(ProgramPurchaseEndReason.AcademicFail, enrollment.EndReason);
        Assert.Equal(_moduleId, enrollment.EndedModuleId);
        Assert.Equal(
            EnrollmentStatus.Failed,
            _db.ModuleEnrollments.Items.Single(me => me.Id == _moduleEnrollmentId).Status);
    }

    [Fact]
    public async Task TryCloseAfterFailedAssignmentAsync_NoOp_WhenAttemptsRemain()
    {
        SeedAcademicContext(maxAttempts: 2);
        SeedFailedSubmission(attemptNumber: 1);
        SeedRecoveryRequest(AssessmentRecoveryRequestStatus.Approved);
        SeedRecoveryRequest(AssessmentRecoveryRequestStatus.Rejected);
        var sut = CreateSut();

        await sut.TryCloseAfterFailedAssignmentAsync(_studentId, _assignmentId, _moduleEnrollmentId);

        var enrollment = _db.ProgramEnrollments.Items.Single(pe => pe.Id == _enrollmentId);
        Assert.Equal(EnrollmentStatus.Active, enrollment.Status);
    }

    [Fact]
    public async Task TryCloseAfterFailedAssignmentAsync_NoOp_WhenRecoveryCapNotReached()
    {
        SeedAcademicContext(maxAttempts: 1);
        SeedFailedSubmission(attemptNumber: 1);
        SeedRecoveryRequest(AssessmentRecoveryRequestStatus.Approved);
        var sut = CreateSut();

        await sut.TryCloseAfterFailedAssignmentAsync(_studentId, _assignmentId, _moduleEnrollmentId);

        var enrollment = _db.ProgramEnrollments.Items.Single(pe => pe.Id == _enrollmentId);
        Assert.Equal(EnrollmentStatus.Active, enrollment.Status);
    }

    [Fact]
    public async Task TryCloseAfterFailedAssignmentAsync_NoOp_WhenLatestPassed()
    {
        SeedAcademicContext(maxAttempts: 1);
        _db.Submissions.Seed(new Submission
        {
            Id = Guid.NewGuid(),
            Code = "SUB-PASS",
            AssignmentId = _assignmentId,
            StudentId = _studentId,
            ModuleEnrollmentId = _moduleEnrollmentId,
            AttemptNumber = 1,
            Status = SubmissionStatus.Graded,
            AssignedGrade = 80,
            IsDeleted = false,
        });
        SeedRecoveryRequest(AssessmentRecoveryRequestStatus.Approved);
        SeedRecoveryRequest(AssessmentRecoveryRequestStatus.Rejected);
        var sut = CreateSut();

        await sut.TryCloseAfterFailedAssignmentAsync(_studentId, _assignmentId, _moduleEnrollmentId);

        var enrollment = _db.ProgramEnrollments.Items.Single(pe => pe.Id == _enrollmentId);
        Assert.Equal(EnrollmentStatus.Active, enrollment.Status);
    }

    [Fact]
    public async Task TryCloseAfterFailedAssignmentAsync_NoOp_WhenTheoryModule()
    {
        SeedAcademicContext(maxAttempts: 1, moduleType: ModuleType.Theory);
        SeedFailedSubmission(attemptNumber: 1);
        SeedRecoveryRequest(AssessmentRecoveryRequestStatus.Approved);
        SeedRecoveryRequest(AssessmentRecoveryRequestStatus.Rejected);
        var sut = CreateSut();

        await sut.TryCloseAfterFailedAssignmentAsync(_studentId, _assignmentId, _moduleEnrollmentId);

        var enrollment = _db.ProgramEnrollments.Items.Single(pe => pe.Id == _enrollmentId);
        Assert.Equal(EnrollmentStatus.Active, enrollment.Status);
    }

    [Fact]
    public async Task TryCloseAfterFailedAssignmentAsync_NoOp_WhenEnrollmentAlreadyTerminal()
    {
        SeedAcademicContext(maxAttempts: 1, programStatus: EnrollmentStatus.Failed);
        SeedFailedSubmission(attemptNumber: 1);
        SeedRecoveryRequest(AssessmentRecoveryRequestStatus.Approved);
        SeedRecoveryRequest(AssessmentRecoveryRequestStatus.Rejected);
        var sut = CreateSut();

        await sut.TryCloseAfterFailedAssignmentAsync(_studentId, _assignmentId, _moduleEnrollmentId);

        var enrollment = _db.ProgramEnrollments.Items.Single(pe => pe.Id == _enrollmentId);
        Assert.Equal(EnrollmentStatus.Failed, enrollment.Status);
        Assert.Null(enrollment.EndReason);
    }

    [Fact]
    public async Task TryCloseAfterFailedAssignmentAsync_NoOp_WhenAssignmentIsOptional()
    {
        SeedAcademicContext(maxAttempts: 1, isRequiredForModulePass: false);
        SeedFailedSubmission(attemptNumber: 1);
        SeedRecoveryRequest(AssessmentRecoveryRequestStatus.Approved);
        SeedRecoveryRequest(AssessmentRecoveryRequestStatus.Rejected);
        var sut = CreateSut();

        await sut.TryCloseAfterFailedAssignmentAsync(_studentId, _assignmentId, _moduleEnrollmentId);

        var enrollment = _db.ProgramEnrollments.Items.Single(pe => pe.Id == _enrollmentId);
        Assert.Equal(EnrollmentStatus.Active, enrollment.Status);
    }

    [Fact]
    public async Task TryCloseAfterFailedAssignmentAsync_NoOp_WhenTurnedInWaitingGrade()
    {
        SeedAcademicContext(maxAttempts: 1);
        SeedFailedSubmission(attemptNumber: 1);
        _db.Submissions.Seed(new Submission
        {
            Id = Guid.NewGuid(),
            Code = "SUB-WAIT",
            AssignmentId = _assignmentId,
            StudentId = _studentId,
            ModuleEnrollmentId = _moduleEnrollmentId,
            AttemptNumber = 2,
            Status = SubmissionStatus.TurnedIn,
            IsDeleted = false,
        });
        SeedRecoveryRequest(AssessmentRecoveryRequestStatus.Approved);
        SeedRecoveryRequest(AssessmentRecoveryRequestStatus.Rejected);
        var sut = CreateSut();

        await sut.TryCloseAfterFailedAssignmentAsync(_studentId, _assignmentId, _moduleEnrollmentId);

        var enrollment = _db.ProgramEnrollments.Items.Single(pe => pe.Id == _enrollmentId);
        Assert.Equal(EnrollmentStatus.Active, enrollment.Status);
    }

    private void SeedElapsedWindowSeat()
    {
        ClassAssignmentWindowSeed.ClassWithActiveEnrollment(
            _db,
            _classId,
            _programId,
            _studentId,
            _enrollmentId);
        ClassAssignmentWindowSeed.Open(
            _db,
            _classId,
            _moduleId,
            _assignmentId,
            start: _now.AddDays(-10),
            end: _now.AddHours(-1));
    }

    [Fact]
    public async Task TryCloseAfterAssignmentWindowElapsedAsync_ClosesTheory_WhenRequiredAndNoDraft()
    {
        SeedAcademicContext(moduleType: ModuleType.Theory);
        SeedElapsedWindowSeat();
        var sut = CreateSut();

        await sut.TryCloseAfterAssignmentWindowElapsedAsync(_studentId, _assignmentId, _moduleEnrollmentId);

        var enrollment = _db.ProgramEnrollments.Items.Single(pe => pe.Id == _enrollmentId);
        Assert.Equal(EnrollmentStatus.Failed, enrollment.Status);
        Assert.Equal(ProgramPurchaseEndReason.AcademicFail, enrollment.EndReason);
    }

    [Fact]
    public async Task TryCloseAfterAssignmentWindowElapsedAsync_NoOp_WhenOptional()
    {
        SeedAcademicContext(moduleType: ModuleType.Theory, isRequiredForModulePass: false);
        SeedElapsedWindowSeat();
        var sut = CreateSut();

        await sut.TryCloseAfterAssignmentWindowElapsedAsync(_studentId, _assignmentId, _moduleEnrollmentId);

        Assert.Equal(
            EnrollmentStatus.Active,
            _db.ProgramEnrollments.Items.Single(pe => pe.Id == _enrollmentId).Status);
    }

    [Fact]
    public async Task TryCloseAfterAssignmentWindowElapsedAsync_NoOp_WhenTurnedIn()
    {
        SeedAcademicContext();
        SeedElapsedWindowSeat();
        _db.Submissions.Seed(new Submission
        {
            Id = Guid.NewGuid(),
            Code = "SUB-TIN",
            AssignmentId = _assignmentId,
            StudentId = _studentId,
            ModuleEnrollmentId = _moduleEnrollmentId,
            AttemptNumber = 1,
            Status = SubmissionStatus.TurnedIn,
            IsDeleted = false,
        });
        var sut = CreateSut();

        await sut.TryCloseAfterAssignmentWindowElapsedAsync(_studentId, _assignmentId, _moduleEnrollmentId);

        Assert.Equal(
            EnrollmentStatus.Active,
            _db.ProgramEnrollments.Items.Single(pe => pe.Id == _enrollmentId).Status);
    }

    [Fact]
    public async Task TryCloseAfterAssignmentWindowElapsedAsync_NoOp_WhenInProgressDraft()
    {
        SeedAcademicContext();
        SeedElapsedWindowSeat();
        _db.Submissions.Seed(new Submission
        {
            Id = Guid.NewGuid(),
            Code = "SUB-DRAFT",
            AssignmentId = _assignmentId,
            StudentId = _studentId,
            ModuleEnrollmentId = _moduleEnrollmentId,
            AttemptNumber = 1,
            Status = SubmissionStatus.Pending,
            ExpiresAt = _now.AddHours(1),
            IsDeleted = false,
        });
        var sut = CreateSut();

        await sut.TryCloseAfterAssignmentWindowElapsedAsync(_studentId, _assignmentId, _moduleEnrollmentId);

        Assert.Equal(
            EnrollmentStatus.Active,
            _db.ProgramEnrollments.Items.Single(pe => pe.Id == _enrollmentId).Status);
    }

    [Fact]
    public async Task TryCloseAfterAssignmentWindowElapsedAsync_Closes_WhenQuizExpiresAtPassed()
    {
        SeedAcademicContext();
        SeedElapsedWindowSeat();
        _db.Submissions.Seed(new Submission
        {
            Id = Guid.NewGuid(),
            Code = "SUB-EXP",
            AssignmentId = _assignmentId,
            StudentId = _studentId,
            ModuleEnrollmentId = _moduleEnrollmentId,
            AttemptNumber = 1,
            Status = SubmissionStatus.Pending,
            ExpiresAt = _now.AddMinutes(-5),
            IsDeleted = false,
        });
        var sut = CreateSut();

        await sut.TryCloseAfterAssignmentWindowElapsedAsync(_studentId, _assignmentId, _moduleEnrollmentId);

        Assert.Equal(
            EnrollmentStatus.Failed,
            _db.ProgramEnrollments.Items.Single(pe => pe.Id == _enrollmentId).Status);
    }

    [Fact]
    public async Task TryCloseAfterAssignmentWindowElapsedAsync_Closes_WhenPendingDraftHasNoExpiresAt()
    {
        SeedAcademicContext();
        SeedElapsedWindowSeat();
        _db.Submissions.Seed(new Submission
        {
            Id = Guid.NewGuid(),
            Code = "SUB-NOTIMER",
            AssignmentId = _assignmentId,
            StudentId = _studentId,
            ModuleEnrollmentId = _moduleEnrollmentId,
            AttemptNumber = 1,
            Status = SubmissionStatus.Pending,
            IsDeleted = false,
        });
        var sut = CreateSut();

        await sut.TryCloseAfterAssignmentWindowElapsedAsync(_studentId, _assignmentId, _moduleEnrollmentId);

        Assert.Equal(
            EnrollmentStatus.Failed,
            _db.ProgramEnrollments.Items.Single(pe => pe.Id == _enrollmentId).Status);
    }

    [Fact]
    public async Task CloseElapsedRequiredWindowsAsync_ClosesActiveSeat()
    {
        SeedAcademicContext(moduleType: ModuleType.Theory);
        SeedElapsedWindowSeat();
        var sut = CreateSut();

        var closed = await sut.CloseElapsedRequiredWindowsAsync();

        Assert.Equal(1, closed);
        Assert.Equal(
            EnrollmentStatus.Failed,
            _db.ProgramEnrollments.Items.Single(pe => pe.Id == _enrollmentId).Status);
    }

    [Fact]
    public async Task TryExtendNextMilestoneWindowAfterPassAsync_ExtendsWhenClosedOrShort()
    {
        SeedAcademicContext(moduleType: ModuleType.Research);
        var nextAssignmentId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaa1");
        _db.Assignments.Seed(new Assignment
        {
            Id = nextAssignmentId,
            Code = "ASM-NEXT",
            Title = "Milestone 2",
            ModuleId = _moduleId,
            AssignmentType = AssignmentType.FileUpload,
            MaxPoints = 100,
            PassScore = 50,
            IsRequiredForModulePass = true,
            IsDeleted = false,
        });
        _db.ResearchMilestones.Seed(
            new ResearchMilestone
            {
                Id = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbb1"),
                Code = "MS-1",
                Title = "Proposal",
                ModuleId = _moduleId,
                AssignmentId = _assignmentId,
                MilestoneOrder = 1,
                IsDeleted = false,
            },
            new ResearchMilestone
            {
                Id = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbb2"),
                Code = "MS-2",
                Title = "Report",
                ModuleId = _moduleId,
                AssignmentId = nextAssignmentId,
                MilestoneOrder = 2,
                IsDeleted = false,
            });
        ClassAssignmentWindowSeed.ClassWithActiveEnrollment(
            _db,
            _classId,
            _programId,
            _studentId,
            _enrollmentId);
        var nextWindow = ClassAssignmentWindowSeed.Open(
            _db,
            _classId,
            _moduleId,
            nextAssignmentId,
            start: _now.AddDays(-10),
            end: _now.AddHours(-1));
        var sut = CreateSut();

        await sut.TryExtendNextMilestoneWindowAfterPassAsync(
            _db.Assignments.Items.Single(a => a.Id == _assignmentId),
            _studentId);

        Assert.Equal(_now.AddHours(ProgramPurchaseLifecycle.NextMilestoneWindowPadHours), nextWindow.EndTime);
    }

    [Fact]
    public void ResolveCreditHint_MapsCopiedRedoAndAhead()
    {
        var taught = new ClassSession
        {
            Status = ClassSessionStatus.Completed,
            EndTime = _now.AddDays(-1),
            IsDeleted = false,
        };
        var upcoming = new ClassSession
        {
            Status = ClassSessionStatus.Scheduled,
            EndTime = _now.AddDays(7),
            IsDeleted = false,
        };

        Assert.Equal(
            RebuyModuleCreditHint.Ahead,
            ProgramPurchaseLifecycle.ResolveCreditHint(false, [taught], _now));
        Assert.Equal(
            RebuyModuleCreditHint.Copied,
            ProgramPurchaseLifecycle.ResolveCreditHint(true, [taught], _now));
        Assert.Equal(
            RebuyModuleCreditHint.RedoWithClass,
            ProgramPurchaseLifecycle.ResolveCreditHint(true, [upcoming], _now));
    }

    // ── Rebuy helpers ────────────────────────────────────────────────────────

    private ProgramEnrollment SeedClosedSource(
        EnrollmentStatus status,
        DateTime? endedAt = null,
        DateTime? completedAt = null,
        Guid? endedModuleId = null)
    {
        var source = new ProgramEnrollment
        {
            Id = Guid.NewGuid(),
            StudentId = _studentId,
            ProgramId = _programId,
            Status = status,
            EndReason = status == EnrollmentStatus.Dropped ? ProgramPurchaseEndReason.Withdraw
                : status == EnrollmentStatus.Failed ? ProgramPurchaseEndReason.AcademicFail
                : null,
            EndedAt = endedAt,
            CompletedAt = completedAt,
            EndedModuleId = endedModuleId,
            IsDeleted = false,
        };
        _db.ProgramEnrollments.Seed(source);
        return source;
    }

    private Program SeedPricedProgram(decimal price = 500_000m, decimal? retakeFee = null)
    {
        var program = new Program
        {
            Id = _programId,
            Code = "PRG-001",
            Name = "Robotics",
            Price = price,
            RetakeFee = retakeFee,
            IsDeleted = false,
        };
        _db.Programs.Seed(program);
        return program;
    }

    [Fact]
    public void IsWithinRebuyWindow_TrueInside_AndOnBoundary_FalseAfter()
    {
        var endedAt = _now.AddMonths(-2);
        var source = new ProgramEnrollment { Status = EnrollmentStatus.Failed, EndedAt = endedAt };

        Assert.True(ProgramPurchaseLifecycle.IsWithinRebuyWindow(source, _now));
        Assert.True(ProgramPurchaseLifecycle.IsWithinRebuyWindow(source, endedAt.AddMonths(3)));
        Assert.False(ProgramPurchaseLifecycle.IsWithinRebuyWindow(source, endedAt.AddMonths(3).AddTicks(1)));
    }

    [Fact]
    public void IsWithinRebuyWindow_CompletedSource_AnchorsAtCompletedAt()
    {
        var source = new ProgramEnrollment
        {
            Status = EnrollmentStatus.Completed,
            CompletedAt = _now.AddMonths(-1),
        };

        Assert.True(ProgramPurchaseLifecycle.IsWithinRebuyWindow(source, _now));
    }

    [Fact]
    public void IsWithinRebuyWindow_False_WhenNoAnchor()
    {
        var source = new ProgramEnrollment { Status = EnrollmentStatus.Failed };

        Assert.False(ProgramPurchaseLifecycle.IsWithinRebuyWindow(source, _now));
    }

    [Fact]
    public async Task ResolveCheckoutAmount_NoSource_ChargesFullPrice()
    {
        var program = SeedPricedProgram(retakeFee: 200_000m);
        var pending = SeedEnrollment(EnrollmentStatus.PendingPayment);
        var sut = CreateSut();

        var amount = await sut.ResolveCheckoutAmountAsync(program, pending);

        Assert.Equal(500_000m, amount);
    }

    [Fact]
    public async Task ResolveCheckoutAmount_RebuyWithinWindow_ChargesRetakeFee()
    {
        var program = SeedPricedProgram(retakeFee: 200_000m);
        var source = SeedClosedSource(EnrollmentStatus.Failed, endedAt: _now.AddMonths(-1));
        var pending = SeedEnrollment(EnrollmentStatus.PendingPayment);
        pending.SourceProgramEnrollmentId = source.Id;
        var sut = CreateSut();

        var amount = await sut.ResolveCheckoutAmountAsync(program, pending);

        Assert.Equal(200_000m, amount);
    }

    [Fact]
    public async Task ResolveCheckoutAmount_RebuyWithinWindow_NoRetakeFee_FallsBackToPrice()
    {
        var program = SeedPricedProgram();
        var source = SeedClosedSource(EnrollmentStatus.Dropped, endedAt: _now.AddDays(-5));
        var pending = SeedEnrollment(EnrollmentStatus.PendingPayment);
        pending.SourceProgramEnrollmentId = source.Id;
        var sut = CreateSut();

        var amount = await sut.ResolveCheckoutAmountAsync(program, pending);

        Assert.Equal(500_000m, amount);
    }

    [Fact]
    public async Task ResolveCheckoutAmount_RebuyOutsideWindow_ChargesFullPrice()
    {
        var program = SeedPricedProgram(retakeFee: 200_000m);
        var source = SeedClosedSource(EnrollmentStatus.Failed, endedAt: _now.AddMonths(-4));
        var pending = SeedEnrollment(EnrollmentStatus.PendingPayment);
        pending.SourceProgramEnrollmentId = source.Id;
        var sut = CreateSut();

        var amount = await sut.ResolveCheckoutAmountAsync(program, pending);

        Assert.Equal(500_000m, amount);
    }

    [Fact]
    public async Task ResolveCheckoutAmount_CompletedSourceWithinWindow_ChargesRetakeFee()
    {
        var program = SeedPricedProgram(retakeFee: 200_000m);
        var source = SeedClosedSource(EnrollmentStatus.Completed, completedAt: _now.AddMonths(-2));
        var pending = SeedEnrollment(EnrollmentStatus.PendingPayment);
        pending.SourceProgramEnrollmentId = source.Id;
        var sut = CreateSut();

        var amount = await sut.ResolveCheckoutAmountAsync(program, pending);

        Assert.Equal(200_000m, amount);
    }

    [Fact]
    public async Task ValidateRebuyClassEligibility_NoSource_AllowsAnyClass()
    {
        var pending = SeedEnrollment(EnrollmentStatus.PendingPayment);
        var sut = CreateSut();

        await sut.ValidateRebuyClassEligibilityAsync(pending, Guid.NewGuid());
    }

    [Fact]
    public async Task ValidateRebuyClassEligibility_CompletedSource_AllowsAnyClass()
    {
        var source = SeedClosedSource(EnrollmentStatus.Completed, completedAt: _now.AddDays(-1));
        var pending = SeedEnrollment(EnrollmentStatus.PendingPayment);
        pending.SourceProgramEnrollmentId = source.Id;
        var sut = CreateSut();

        await sut.ValidateRebuyClassEligibilityAsync(pending, Guid.NewGuid());
    }

    [Fact]
    public async Task ValidateRebuyClassEligibility_Throws_WhenClassIsSourceClass()
    {
        var classId = Guid.NewGuid();
        var source = SeedClosedSource(EnrollmentStatus.Failed, endedAt: _now.AddDays(-1));
        _db.ClassEnrollments.Seed(new ClassEnrollment
        {
            Id = Guid.NewGuid(),
            ClassId = classId,
            StudentId = _studentId,
            ProgramEnrollmentId = source.Id,
            Status = ClassEnrollmentStatus.Withdrawn,
            IsDeleted = false,
        });
        var pending = SeedEnrollment(EnrollmentStatus.PendingPayment);
        pending.SourceProgramEnrollmentId = source.Id;
        var sut = CreateSut();

        var ex = await Assert.ThrowsAsync<BadRequestException>(() =>
            sut.ValidateRebuyClassEligibilityAsync(pending, classId));
        Assert.Equal(ProgramPurchaseLifecycle.RebuySameClassMessage, ex.Message);
    }

    [Fact]
    public async Task ValidateRebuyClassEligibility_CompletedSource_StillBlocksSourceClass()
    {
        var classId = Guid.NewGuid();
        var source = SeedClosedSource(EnrollmentStatus.Completed, completedAt: _now.AddDays(-1));
        _db.ClassEnrollments.Seed(new ClassEnrollment
        {
            Id = Guid.NewGuid(),
            ClassId = classId,
            StudentId = _studentId,
            ProgramEnrollmentId = source.Id,
            Status = ClassEnrollmentStatus.Completed,
            IsDeleted = false,
        });
        var pending = SeedEnrollment(EnrollmentStatus.PendingPayment);
        pending.SourceProgramEnrollmentId = source.Id;
        var sut = CreateSut();

        var ex = await Assert.ThrowsAsync<BadRequestException>(() =>
            sut.ValidateRebuyClassEligibilityAsync(pending, classId));
        Assert.Equal(ProgramPurchaseLifecycle.RebuySameClassMessage, ex.Message);
    }

    private Module SeedModule(string code, int order)
    {
        var module = new Module
        {
            Id = Guid.NewGuid(),
            Code = code,
            Name = code,
            ProgramId = _programId,
            ModuleType = ModuleType.Experiential,
            ModuleOrder = order,
            IsDeleted = false,
        };
        _db.Modules.Seed(module);
        return module;
    }

    private void SeedModuleEnrollment(Guid programEnrollmentId, Guid moduleId, EnrollmentStatus status)
    {
        _db.ModuleEnrollments.Seed(new ModuleEnrollment
        {
            Id = Guid.NewGuid(),
            StudentId = _studentId,
            ModuleId = moduleId,
            ProgramEnrollmentId = programEnrollmentId,
            Status = status,
            IsDeleted = false,
        });
    }

    private void SeedSession(Guid classId, Guid moduleId, ClassSessionStatus status)
    {
        var (start, end) = DefaultSessionWindow(status);
        _db.ClassSessions.Seed(new ClassSession
        {
            Id = Guid.NewGuid(),
            ClassId = classId,
            ModuleId = moduleId,
            Title = "Session",
            StartTime = start,
            EndTime = end,
            Status = status,
            IsDeleted = false,
        });
    }

    private void SeedSessionAt(
        Guid classId,
        Guid moduleId,
        ClassSessionStatus status,
        DateTime start,
        DateTime end,
        Guid? activityId = null,
        Guid? assignmentId = null)
    {
        _db.ClassSessions.Seed(new ClassSession
        {
            Id = Guid.NewGuid(),
            ClassId = classId,
            ModuleId = moduleId,
            ActivityId = activityId,
            AssignmentId = assignmentId,
            Title = "Session",
            StartTime = start,
            EndTime = end,
            Status = status,
            IsDeleted = false,
        });
    }

    private (DateTime Start, DateTime End) DefaultSessionWindow(ClassSessionStatus status)
    {
        if (status == ClassSessionStatus.Completed)
        {
            var start = _now.AddDays(-2);
            return (start, start.AddHours(2));
        }

        if (status == ClassSessionStatus.InProgress)
        {
            return (_now.AddHours(-1), _now.AddHours(1));
        }

        var future = _now.AddDays(7);
        return (future, future.AddHours(2));
    }

    /// <summary>Seeds the class seat the rebuy student holds on the new class.</summary>
    private Guid SeedNewClassSeat(
        Guid programEnrollmentId,
        ClassEnrollmentStatus status = ClassEnrollmentStatus.Active)
    {
        var classId = Guid.NewGuid();
        _db.ClassEnrollments.Seed(new ClassEnrollment
        {
            Id = Guid.NewGuid(),
            ClassId = classId,
            StudentId = _studentId,
            ProgramEnrollmentId = programEnrollmentId,
            Status = status,
            IsDeleted = false,
        });
        return classId;
    }

    private void SeedCurriculumActivity(Guid moduleId, Guid activityId, string code)
    {
        var course = _db.Courses.Items.FirstOrDefault(c => c.ModuleId == moduleId && !c.IsDeleted);
        Guid courseId;
        if (course == null)
        {
            courseId = Guid.NewGuid();
            _db.Courses.Seed(new Course
            {
                Id = courseId,
                Code = "CRS-" + code,
                Name = "CRS-" + code,
                ModuleId = moduleId,
                IsDeleted = false,
            });
        }
        else
        {
            courseId = course.Id;
        }

        _db.Activities.Seed(new Activity
        {
            Id = activityId,
            Code = code,
            Name = code,
            CourseId = courseId,
            IsDeleted = false,
        });
    }

    private void SeedSessionForActivity(
        Guid classId,
        Guid moduleId,
        ClassSessionStatus status,
        Guid? activityId = null,
        Guid? assignmentId = null)
    {
        var (start, end) = DefaultSessionWindow(status);
        _db.ClassSessions.Seed(new ClassSession
        {
            Id = Guid.NewGuid(),
            ClassId = classId,
            ModuleId = moduleId,
            ActivityId = activityId,
            AssignmentId = assignmentId,
            Title = "Session",
            StartTime = start,
            EndTime = end,
            Status = status,
            IsDeleted = false,
        });
    }

    [Theory]
    [InlineData(ClassSessionStatus.InProgress)]
    [InlineData(ClassSessionStatus.Completed)]
    public async Task ValidateRebuyClassEligibility_Throws_WhenClassStartedFailedModule(ClassSessionStatus sessionStatus)
    {
        var classId = Guid.NewGuid();
        var module = SeedModule("MOD-A", 1);
        var source = SeedClosedSource(EnrollmentStatus.Failed, endedAt: _now.AddDays(-1), endedModuleId: module.Id);
        var pending = SeedEnrollment(EnrollmentStatus.PendingPayment);
        pending.SourceProgramEnrollmentId = source.Id;
        SeedSession(classId, module.Id, sessionStatus);
        var sut = CreateSut();

        await Assert.ThrowsAsync<BadRequestException>(() =>
            sut.ValidateRebuyClassEligibilityAsync(pending, classId));
    }

    [Fact]
    public async Task ValidateRebuyClassEligibility_Throws_WhenClassStartedLaterModule()
    {
        var classId = Guid.NewGuid();
        var moduleA = SeedModule("MOD-A", 1);
        var moduleB = SeedModule("MOD-B", 2);
        var source = SeedClosedSource(EnrollmentStatus.Failed, endedAt: _now.AddDays(-1), endedModuleId: moduleA.Id);
        var pending = SeedEnrollment(EnrollmentStatus.PendingPayment);
        pending.SourceProgramEnrollmentId = source.Id;
        SeedSession(classId, moduleB.Id, ClassSessionStatus.InProgress);
        var sut = CreateSut();

        await Assert.ThrowsAsync<BadRequestException>(() =>
            sut.ValidateRebuyClassEligibilityAsync(pending, classId));
    }

    [Fact]
    public async Task ValidateRebuyClassEligibility_Allows_WhenClassOnlyStartedEarlierModule()
    {
        var classId = Guid.NewGuid();
        var moduleA = SeedModule("MOD-A", 1);
        var moduleB = SeedModule("MOD-B", 2);
        var source = SeedClosedSource(EnrollmentStatus.Failed, endedAt: _now.AddDays(-1), endedModuleId: moduleB.Id);
        var pending = SeedEnrollment(EnrollmentStatus.PendingPayment);
        pending.SourceProgramEnrollmentId = source.Id;
        SeedSession(classId, moduleA.Id, ClassSessionStatus.Completed);
        var sut = CreateSut();

        await sut.ValidateRebuyClassEligibilityAsync(pending, classId);
    }

    [Fact]
    public async Task ValidateRebuyClassEligibility_Allows_WhenFailedModuleOnlyScheduled()
    {
        var classId = Guid.NewGuid();
        var module = SeedModule("MOD-A", 1);
        var source = SeedClosedSource(EnrollmentStatus.Failed, endedAt: _now.AddDays(-1), endedModuleId: module.Id);
        var pending = SeedEnrollment(EnrollmentStatus.PendingPayment);
        pending.SourceProgramEnrollmentId = source.Id;
        SeedSession(classId, module.Id, ClassSessionStatus.Scheduled);
        var sut = CreateSut();

        await sut.ValidateRebuyClassEligibilityAsync(pending, classId);
    }

    [Fact]
    public async Task ValidateRebuyClassEligibility_Withdraw_Throws_WhenClassStartedStopModule()
    {
        var classId = Guid.NewGuid();
        var moduleA = SeedModule("MOD-A", 1);
        var moduleB = SeedModule("MOD-B", 2);
        var source = SeedClosedSource(EnrollmentStatus.Dropped, endedAt: _now.AddDays(-1));
        SeedModuleEnrollment(source.Id, moduleA.Id, EnrollmentStatus.Completed);
        SeedModuleEnrollment(source.Id, moduleB.Id, EnrollmentStatus.Active);
        var pending = SeedEnrollment(EnrollmentStatus.PendingPayment);
        pending.SourceProgramEnrollmentId = source.Id;
        SeedSession(classId, moduleB.Id, ClassSessionStatus.InProgress);
        var sut = CreateSut();

        await Assert.ThrowsAsync<BadRequestException>(() =>
            sut.ValidateRebuyClassEligibilityAsync(pending, classId));
    }

    [Fact]
    public async Task ValidateRebuyClassEligibility_Withdraw_Allows_WhenClassOnlyStartedCompletedModule()
    {
        var classId = Guid.NewGuid();
        var moduleA = SeedModule("MOD-A", 1);
        var moduleB = SeedModule("MOD-B", 2);
        var source = SeedClosedSource(EnrollmentStatus.Dropped, endedAt: _now.AddDays(-1));
        SeedModuleEnrollment(source.Id, moduleA.Id, EnrollmentStatus.Completed);
        SeedModuleEnrollment(source.Id, moduleB.Id, EnrollmentStatus.Active);
        var pending = SeedEnrollment(EnrollmentStatus.PendingPayment);
        pending.SourceProgramEnrollmentId = source.Id;
        SeedSession(classId, moduleA.Id, ClassSessionStatus.Completed);
        var sut = CreateSut();

        await sut.ValidateRebuyClassEligibilityAsync(pending, classId);
    }

    private ModuleEnrollment SeedCompletedSourceModuleEnrollment(
        Guid sourceEnrollmentId,
        Guid moduleId,
        int attemptNumber = 1,
        decimal? finalGrade = 8m)
    {
        var moduleEnrollment = new ModuleEnrollment
        {
            Id = Guid.NewGuid(),
            StudentId = _studentId,
            ModuleId = moduleId,
            ProgramEnrollmentId = sourceEnrollmentId,
            Status = EnrollmentStatus.Completed,
            ProgressPercent = 100m,
            FinalGrade = finalGrade,
            AttemptNumber = attemptNumber,
            IsDeleted = false,
        };
        _db.ModuleEnrollments.Seed(moduleEnrollment);
        return moduleEnrollment;
    }

    [Fact]
    public async Task ApplyRebuyCredits_NoSource_CopiesNothing()
    {
        var pending = SeedEnrollment(EnrollmentStatus.Active);
        var sut = CreateSut();

        await sut.ApplyRebuyCreditsAsync(pending);

        Assert.Empty(_db.ModuleEnrollments.Items);
    }

    [Fact]
    public async Task ApplyRebuyCredits_CompletedSource_CopiesNothing()
    {
        var source = SeedClosedSource(EnrollmentStatus.Completed, completedAt: _now.AddDays(-1));
        var module = SeedModule("MOD-A", 1);
        SeedCompletedSourceModuleEnrollment(source.Id, module.Id);
        var pending = SeedEnrollment(EnrollmentStatus.Active);
        pending.SourceProgramEnrollmentId = source.Id;
        var sut = CreateSut();

        await sut.ApplyRebuyCreditsAsync(pending);

        Assert.Single(_db.ModuleEnrollments.Items);
    }

    [Fact]
    public async Task ApplyRebuyCredits_OutsideWindow_CopiesNothing()
    {
        var source = SeedClosedSource(EnrollmentStatus.Failed, endedAt: _now.AddMonths(-4));
        var module = SeedModule("MOD-A", 1);
        SeedCompletedSourceModuleEnrollment(source.Id, module.Id);
        var pending = SeedEnrollment(EnrollmentStatus.Active);
        pending.SourceProgramEnrollmentId = source.Id;
        var sut = CreateSut();

        await sut.ApplyRebuyCreditsAsync(pending);

        Assert.Single(_db.ModuleEnrollments.Items);
    }

    [Fact]
    public async Task ApplyRebuyCredits_InsideWindow_CopiesCompletedModuleWithProgressAndGradedSubmissions()
    {
        var source = SeedClosedSource(EnrollmentStatus.Failed, endedAt: _now.AddDays(-10));
        var module = SeedModule("MOD-A", 1);
        var sourceModuleEnrollment = SeedCompletedSourceModuleEnrollment(source.Id, module.Id);
        var activityId = Guid.NewGuid();
        SeedCurriculumActivity(module.Id, activityId, "ACT-1");
        _db.ActivityProgresses.Seed(new ActivityProgress
        {
            Id = Guid.NewGuid(),
            StudentId = _studentId,
            ActivityId = activityId,
            ModuleEnrollmentId = sourceModuleEnrollment.Id,
            ActivityStatus = ActivityStatus.Done,
            IsCompleted = true,
            CompletedAt = _now.AddDays(-20),
            IsDeleted = false,
        });
        _db.Submissions.Seed(new Submission
        {
            Id = Guid.NewGuid(),
            Code = "SUB-SOURCE01",
            AssignmentId = Guid.NewGuid(),
            StudentId = _studentId,
            ModuleEnrollmentId = sourceModuleEnrollment.Id,
            AttemptNumber = 2,
            Status = SubmissionStatus.Graded,
            AssignedGrade = 9m,
            SubmittedAt = _now.AddDays(-15),
            GradedAt = _now.AddDays(-14),
            IsDeleted = false,
        });
        _db.Submissions.Seed(new Submission
        {
            Id = Guid.NewGuid(),
            Code = "SUB-SOURCE02",
            AssignmentId = Guid.NewGuid(),
            StudentId = _studentId,
            ModuleEnrollmentId = sourceModuleEnrollment.Id,
            AttemptNumber = 1,
            Status = SubmissionStatus.ReturnedForRevision,
            IsDeleted = false,
        });
        var pending = SeedEnrollment(EnrollmentStatus.Active);
        pending.SourceProgramEnrollmentId = source.Id;
        var newClassId = SeedNewClassSeat(pending.Id);
        SeedSession(newClassId, module.Id, ClassSessionStatus.Completed);
        var sut = CreateSut();

        await sut.ApplyRebuyCreditsAsync(pending);

        var copied = Assert.Single(_db.ModuleEnrollments.Items, me => me.Id != sourceModuleEnrollment.Id);
        Assert.Equal(pending.Id, copied.ProgramEnrollmentId);
        Assert.Equal(EnrollmentStatus.Completed, copied.Status);
        Assert.Equal(100m, copied.ProgressPercent);
        Assert.Equal(8m, copied.FinalGrade);
        Assert.Equal(2, copied.AttemptNumber);
        Assert.NotNull(copied.CompletedAt);

        var copiedProgress = Assert.Single(
            _db.ActivityProgresses.Items,
            ap => ap.ModuleEnrollmentId == copied.Id);
        Assert.Equal(activityId, copiedProgress.ActivityId);
        Assert.True(copiedProgress.IsCompleted);

        var copiedSubmission = Assert.Single(
            _db.Submissions.Items,
            s => s.ModuleEnrollmentId == copied.Id);
        Assert.Equal(SubmissionStatus.Graded, copiedSubmission.Status);
        Assert.Equal(9m, copiedSubmission.AssignedGrade);
        Assert.NotEqual("SUB-SOURCE01", copiedSubmission.Code);
        Assert.Equal(2, copiedSubmission.AttemptNumber);
    }

    [Fact]
    public async Task ApplyRebuyCredits_CopiesWholeModule_WhenOpenAssignmentWindowDoesNotBlockTaughtLives()
    {
        var source = SeedClosedSource(EnrollmentStatus.Failed, endedAt: _now.AddDays(-10));
        var module = SeedModule("MOD-A", 1);
        var sourceModuleEnrollment = SeedCompletedSourceModuleEnrollment(source.Id, module.Id);
        var pending = SeedEnrollment(EnrollmentStatus.Active);
        pending.SourceProgramEnrollmentId = source.Id;
        var newClassId = SeedNewClassSeat(pending.Id);
        SeedSession(newClassId, module.Id, ClassSessionStatus.Completed);
        _db.ClassSessions.Seed(new ClassSession
        {
            Id = Guid.NewGuid(),
            ClassId = newClassId,
            ModuleId = module.Id,
            AssignmentId = Guid.NewGuid(),
            Title = "Work window",
            SessionKind = SessionKind.AssignmentWindow,
            StartTime = _now.AddHours(-1),
            EndTime = _now.AddDays(2),
            Status = ClassSessionStatus.InProgress,
            IsDeleted = false,
        });
        var sut = CreateSut();

        await sut.ApplyRebuyCreditsAsync(pending);

        var copied = Assert.Single(_db.ModuleEnrollments.Items, me => me.Id != sourceModuleEnrollment.Id);
        Assert.Equal(pending.Id, copied.ProgramEnrollmentId);
    }

    [Fact]
    public async Task ApplyRebuyCredits_SkipsNonCompletedModules()
    {
        var source = SeedClosedSource(EnrollmentStatus.Failed, endedAt: _now.AddDays(-5));
        var moduleA = SeedModule("MOD-A", 1);
        var moduleB = SeedModule("MOD-B", 2);
        SeedCompletedSourceModuleEnrollment(source.Id, moduleA.Id);
        SeedModuleEnrollment(source.Id, moduleB.Id, EnrollmentStatus.Failed);
        var pending = SeedEnrollment(EnrollmentStatus.Active);
        pending.SourceProgramEnrollmentId = source.Id;
        var newClassId = SeedNewClassSeat(pending.Id);
        SeedSession(newClassId, moduleA.Id, ClassSessionStatus.Completed);
        SeedSession(newClassId, moduleB.Id, ClassSessionStatus.Completed);
        var sut = CreateSut();

        await sut.ApplyRebuyCreditsAsync(pending);

        var copied = Assert.Single(
            _db.ModuleEnrollments.Items,
            me => me.ProgramEnrollmentId == pending.Id);
        Assert.Equal(moduleA.Id, copied.ModuleId);
    }

    [Fact]
    public async Task ApplyRebuyCredits_Idempotent_WhenModuleAlreadyOnNewEnrollment()
    {
        var source = SeedClosedSource(EnrollmentStatus.Failed, endedAt: _now.AddDays(-5));
        var module = SeedModule("MOD-A", 1);
        SeedCompletedSourceModuleEnrollment(source.Id, module.Id);
        var pending = SeedEnrollment(EnrollmentStatus.Active);
        pending.SourceProgramEnrollmentId = source.Id;
        SeedModuleEnrollment(pending.Id, module.Id, EnrollmentStatus.Completed);
        var newClassId = SeedNewClassSeat(pending.Id);
        SeedSession(newClassId, module.Id, ClassSessionStatus.Completed);
        var sut = CreateSut();

        await sut.ApplyRebuyCreditsAsync(pending);

        Assert.Equal(
            2,
            _db.ModuleEnrollments.Items.Count(me => me.ModuleId == module.Id));
    }

    [Fact]
    public async Task ApplyRebuyCredits_Copies_WhenSeatHoldIsStillPending()
    {
        var source = SeedClosedSource(EnrollmentStatus.Failed, endedAt: _now.AddDays(-5));
        var module = SeedModule("MOD-A", 1);
        var sourceModuleEnrollment = SeedCompletedSourceModuleEnrollment(source.Id, module.Id);
        var activityId = Guid.NewGuid();
        SeedCurriculumActivity(module.Id, activityId, "ACT-1");
        _db.ActivityProgresses.Seed(new ActivityProgress
        {
            Id = Guid.NewGuid(),
            StudentId = _studentId,
            ActivityId = activityId,
            ModuleEnrollmentId = sourceModuleEnrollment.Id,
            ActivityStatus = ActivityStatus.Done,
            IsCompleted = true,
            IsDeleted = false,
        });
        var pending = SeedEnrollment(EnrollmentStatus.Active);
        pending.SourceProgramEnrollmentId = source.Id;
        var newClassId = SeedNewClassSeat(pending.Id, ClassEnrollmentStatus.Pending);
        SeedSession(newClassId, module.Id, ClassSessionStatus.Completed);
        var sut = CreateSut();

        await sut.ApplyRebuyCreditsAsync(pending);

        var copied = Assert.Single(
            _db.ModuleEnrollments.Items,
            me => me.ProgramEnrollmentId == pending.Id);
        Assert.Equal(module.Id, copied.ModuleId);
        Assert.Equal(EnrollmentStatus.Completed, copied.Status);
        Assert.Equal(sourceModuleEnrollment.ModuleId, copied.ModuleId);
    }

    [Fact]
    public async Task ApplyRebuyCredits_CopiesNothing_WhenNoClassSeat()
    {
        var source = SeedClosedSource(EnrollmentStatus.Failed, endedAt: _now.AddDays(-5));
        var module = SeedModule("MOD-A", 1);
        SeedCompletedSourceModuleEnrollment(source.Id, module.Id);
        var pending = SeedEnrollment(EnrollmentStatus.Active);
        pending.SourceProgramEnrollmentId = source.Id;
        var sut = CreateSut();

        await sut.ApplyRebuyCreditsAsync(pending);

        Assert.DoesNotContain(
            _db.ModuleEnrollments.Items,
            me => me.ProgramEnrollmentId == pending.Id);
    }

    [Fact]
    public async Task ApplyRebuyCredits_SkipsModule_WhenNewClassHasNotStartedIt()
    {
        var source = SeedClosedSource(EnrollmentStatus.Failed, endedAt: _now.AddDays(-5));
        var module = SeedModule("MOD-A", 1);
        SeedCompletedSourceModuleEnrollment(source.Id, module.Id);
        var pending = SeedEnrollment(EnrollmentStatus.Active);
        pending.SourceProgramEnrollmentId = source.Id;
        var newClassId = SeedNewClassSeat(pending.Id);
        // New class has only a Scheduled session for this module — nothing taught yet.
        SeedSession(newClassId, module.Id, ClassSessionStatus.Scheduled);
        var sut = CreateSut();

        await sut.ApplyRebuyCreditsAsync(pending);

        Assert.DoesNotContain(
            _db.ModuleEnrollments.Items,
            me => me.ProgramEnrollmentId == pending.Id);
    }

    [Fact]
    public async Task ApplyRebuyCredits_CopiesWholeModule_WhenScheduledSessionsAlreadyEnded()
    {
        var source = SeedClosedSource(EnrollmentStatus.Failed, endedAt: _now.AddDays(-5));
        var module = SeedModule("MOD-A", 1);
        var sourceModuleEnrollment = SeedCompletedSourceModuleEnrollment(source.Id, module.Id);
        var activityId = Guid.NewGuid();
        SeedCurriculumActivity(module.Id, activityId, "ACT-A");
        _db.ActivityProgresses.Seed(new ActivityProgress
        {
            Id = Guid.NewGuid(),
            StudentId = _studentId,
            ActivityId = activityId,
            ModuleEnrollmentId = sourceModuleEnrollment.Id,
            ActivityStatus = ActivityStatus.Done,
            IsCompleted = true,
            IsDeleted = false,
        });
        var pending = SeedEnrollment(EnrollmentStatus.Active);
        pending.SourceProgramEnrollmentId = source.Id;
        var newClassId = SeedNewClassSeat(pending.Id);
        SeedSessionAt(
            newClassId,
            module.Id,
            ClassSessionStatus.Scheduled,
            _now.AddDays(-3),
            _now.AddDays(-3).AddHours(2),
            activityId);
        var sut = CreateSut();

        await sut.ApplyRebuyCreditsAsync(pending);

        var copied = Assert.Single(
            _db.ModuleEnrollments.Items,
            me => me.ProgramEnrollmentId == pending.Id);
        Assert.Equal(EnrollmentStatus.Completed, copied.Status);
        Assert.Contains(
            _db.ActivityProgresses.Items,
            ap => ap.ModuleEnrollmentId == copied.Id && ap.ActivityId == activityId);
    }

    [Fact]
    public async Task ApplyRebuyCredits_PartialModule_CopiesPastScheduledAndSkipsFuture()
    {
        var source = SeedClosedSource(EnrollmentStatus.Failed, endedAt: _now.AddDays(-5));
        var module = SeedModule("MOD-A", 1);
        var sourceModuleEnrollment = SeedCompletedSourceModuleEnrollment(source.Id, module.Id);
        var doneActivityId = Guid.NewGuid();
        var pendingActivityId = Guid.NewGuid();
        SeedCurriculumActivity(module.Id, doneActivityId, "ACT-DONE");
        SeedCurriculumActivity(module.Id, pendingActivityId, "ACT-TODO");
        _db.ActivityProgresses.Seed(new ActivityProgress
        {
            Id = Guid.NewGuid(),
            StudentId = _studentId,
            ActivityId = doneActivityId,
            ModuleEnrollmentId = sourceModuleEnrollment.Id,
            ActivityStatus = ActivityStatus.Done,
            IsCompleted = true,
            IsDeleted = false,
        });
        _db.ActivityProgresses.Seed(new ActivityProgress
        {
            Id = Guid.NewGuid(),
            StudentId = _studentId,
            ActivityId = pendingActivityId,
            ModuleEnrollmentId = sourceModuleEnrollment.Id,
            ActivityStatus = ActivityStatus.Done,
            IsCompleted = true,
            IsDeleted = false,
        });
        var pending = SeedEnrollment(EnrollmentStatus.Active);
        pending.SourceProgramEnrollmentId = source.Id;
        var newClassId = SeedNewClassSeat(pending.Id);
        SeedSessionAt(
            newClassId,
            module.Id,
            ClassSessionStatus.Scheduled,
            _now.AddDays(-2),
            _now.AddDays(-2).AddHours(2),
            doneActivityId);
        SeedSessionAt(
            newClassId,
            module.Id,
            ClassSessionStatus.Scheduled,
            _now.AddDays(5),
            _now.AddDays(5).AddHours(2),
            pendingActivityId);
        var sut = CreateSut();

        await sut.ApplyRebuyCreditsAsync(pending);

        var copied = Assert.Single(
            _db.ModuleEnrollments.Items,
            me => me.ProgramEnrollmentId == pending.Id);
        Assert.Equal(EnrollmentStatus.Active, copied.Status);
        Assert.Contains(
            _db.ActivityProgresses.Items,
            ap => ap.ModuleEnrollmentId == copied.Id && ap.ActivityId == doneActivityId);
        Assert.DoesNotContain(
            _db.ActivityProgresses.Items,
            ap => ap.ModuleEnrollmentId == copied.Id && ap.ActivityId == pendingActivityId);
    }

    [Fact]
    public async Task ApplyRebuyCredits_PartialModule_CopiesOnlyCompletedActivitiesAndStaysActive()
    {
        var source = SeedClosedSource(EnrollmentStatus.Failed, endedAt: _now.AddDays(-5));
        var module = SeedModule("MOD-A", 1);
        var sourceModuleEnrollment = SeedCompletedSourceModuleEnrollment(source.Id, module.Id);

        var courseId = Guid.NewGuid();
        _db.Courses.Seed(new Course
        {
            Id = courseId,
            Code = "CRS-A",
            Name = "CRS-A",
            ModuleId = module.Id,
            IsDeleted = false,
        });
        var doneActivityId = Guid.NewGuid();
        var pendingActivityId = Guid.NewGuid();
        _db.Activities.Seed(
            new Activity { Id = doneActivityId, Code = "ACT-1", Name = "ACT-1", CourseId = courseId, IsDeleted = false },
            new Activity { Id = pendingActivityId, Code = "ACT-2", Name = "ACT-2", CourseId = courseId, IsDeleted = false });

        _db.ActivityProgresses.Seed(
            new ActivityProgress
            {
                Id = Guid.NewGuid(),
                StudentId = _studentId,
                ActivityId = doneActivityId,
                ModuleEnrollmentId = sourceModuleEnrollment.Id,
                ActivityStatus = ActivityStatus.Done,
                IsCompleted = true,
                IsDeleted = false,
            },
            new ActivityProgress
            {
                Id = Guid.NewGuid(),
                StudentId = _studentId,
                ActivityId = pendingActivityId,
                ModuleEnrollmentId = sourceModuleEnrollment.Id,
                ActivityStatus = ActivityStatus.Done,
                IsCompleted = true,
                IsDeleted = false,
            });

        var pending = SeedEnrollment(EnrollmentStatus.Active);
        pending.SourceProgramEnrollmentId = source.Id;
        var newClassId = SeedNewClassSeat(pending.Id);
        // New class finished the session for doneActivity but is still running pendingActivity.
        SeedSessionForActivity(newClassId, module.Id, ClassSessionStatus.Completed, activityId: doneActivityId);
        SeedSessionForActivity(newClassId, module.Id, ClassSessionStatus.InProgress, activityId: pendingActivityId);
        var sut = CreateSut();

        await sut.ApplyRebuyCreditsAsync(pending);

        var copied = Assert.Single(
            _db.ModuleEnrollments.Items,
            me => me.ProgramEnrollmentId == pending.Id);
        Assert.Equal(EnrollmentStatus.Active, copied.Status);
        Assert.Equal(50m, copied.ProgressPercent);
        Assert.Null(copied.FinalGrade);
        Assert.Null(copied.CompletedAt);

        // Only the activity the new class completed a session for is copied.
        var copiedProgress = Assert.Single(
            _db.ActivityProgresses.Items,
            ap => ap.ModuleEnrollmentId == copied.Id);
        Assert.Equal(doneActivityId, copiedProgress.ActivityId);
    }

    [Fact]
    public async Task ApplyRebuyCredits_CopiesWholeModule_WhenNewClassHasNoSessions()
    {
        var source = SeedClosedSource(EnrollmentStatus.Failed, endedAt: _now.AddDays(-5));
        var module = SeedModule("MOD-A", 1);
        var sourceModuleEnrollment = SeedCompletedSourceModuleEnrollment(source.Id, module.Id);
        var activityId = Guid.NewGuid();
        SeedCurriculumActivity(module.Id, activityId, "ACT-SP");
        _db.ActivityProgresses.Seed(new ActivityProgress
        {
            Id = Guid.NewGuid(),
            StudentId = _studentId,
            ActivityId = activityId,
            ModuleEnrollmentId = sourceModuleEnrollment.Id,
            ActivityStatus = ActivityStatus.Done,
            IsCompleted = true,
            IsDeleted = false,
        });
        var pending = SeedEnrollment(EnrollmentStatus.Active);
        pending.SourceProgramEnrollmentId = source.Id;
        SeedNewClassSeat(pending.Id);
        var sut = CreateSut();

        await sut.ApplyRebuyCreditsAsync(pending);

        var copied = Assert.Single(
            _db.ModuleEnrollments.Items,
            me => me.ProgramEnrollmentId == pending.Id);
        Assert.Equal(EnrollmentStatus.Completed, copied.Status);
        Assert.Equal(100m, copied.ProgressPercent);
        Assert.Equal(8m, copied.FinalGrade);
    }

    [Fact]
    public async Task ApplyRebuyCredits_PartialProgress_UsesActivityAndRequiredAssignmentUnits()
    {
        var source = SeedClosedSource(EnrollmentStatus.Failed, endedAt: _now.AddDays(-5));
        var module = SeedModule("MOD-A", 1);
        var sourceModuleEnrollment = SeedCompletedSourceModuleEnrollment(source.Id, module.Id);
        var courseId = Guid.NewGuid();
        _db.Courses.Seed(new Course
        {
            Id = courseId,
            Code = "CRS-A",
            Name = "CRS-A",
            ModuleId = module.Id,
            IsDeleted = false,
        });
        var doneActivityId = Guid.NewGuid();
        var pendingActivityId = Guid.NewGuid();
        _db.Activities.Seed(
            new Activity { Id = doneActivityId, Code = "ACT-1", Name = "ACT-1", CourseId = courseId, IsDeleted = false },
            new Activity { Id = pendingActivityId, Code = "ACT-2", Name = "ACT-2", CourseId = courseId, IsDeleted = false });
        var assignmentId = Guid.NewGuid();
        _db.Assignments.Seed(new Assignment
        {
            Id = assignmentId,
            Code = "ASG-A",
            Title = "Quiz",
            ModuleId = module.Id,
            CourseId = courseId,
            PassScore = 5m,
            IsRequiredForModulePass = true,
            IsDeleted = false,
        });
        _db.ActivityProgresses.Seed(
            new ActivityProgress
            {
                Id = Guid.NewGuid(),
                StudentId = _studentId,
                ActivityId = doneActivityId,
                ModuleEnrollmentId = sourceModuleEnrollment.Id,
                ActivityStatus = ActivityStatus.Done,
                IsCompleted = true,
                IsDeleted = false,
            },
            new ActivityProgress
            {
                Id = Guid.NewGuid(),
                StudentId = _studentId,
                ActivityId = pendingActivityId,
                ModuleEnrollmentId = sourceModuleEnrollment.Id,
                ActivityStatus = ActivityStatus.Done,
                IsCompleted = true,
                IsDeleted = false,
            });
        _db.Submissions.Seed(new Submission
        {
            Id = Guid.NewGuid(),
            Code = "SUB-SRC",
            AssignmentId = assignmentId,
            StudentId = _studentId,
            ModuleEnrollmentId = sourceModuleEnrollment.Id,
            AttemptNumber = 1,
            Status = SubmissionStatus.Graded,
            AssignedGrade = 9m,
            IsDeleted = false,
        });

        var pending = SeedEnrollment(EnrollmentStatus.Active);
        pending.SourceProgramEnrollmentId = source.Id;
        var newClassId = SeedNewClassSeat(pending.Id);
        SeedSessionForActivity(newClassId, module.Id, ClassSessionStatus.Completed, activityId: doneActivityId);
        SeedSessionForActivity(newClassId, module.Id, ClassSessionStatus.InProgress, activityId: pendingActivityId);
        var sut = CreateSut();

        await sut.ApplyRebuyCreditsAsync(pending);

        var copied = Assert.Single(
            _db.ModuleEnrollments.Items,
            me => me.ProgramEnrollmentId == pending.Id);
        Assert.Equal(EnrollmentStatus.Active, copied.Status);
        Assert.Equal(33.33m, copied.ProgressPercent);
        Assert.DoesNotContain(
            _db.Submissions.Items,
            s => s.ModuleEnrollmentId == copied.Id);
    }

    [Fact]
    public async Task ApplyRebuyCredits_RecalculatesProgramProgress()
    {
        var source = SeedClosedSource(EnrollmentStatus.Failed, endedAt: _now.AddDays(-5));
        var moduleA = SeedModule("MOD-A", 1);
        var moduleB = SeedModule("MOD-B", 2);
        var sourceModuleEnrollment = SeedCompletedSourceModuleEnrollment(source.Id, moduleA.Id);
        var activityA = Guid.NewGuid();
        var activityB = Guid.NewGuid();
        SeedCurriculumActivity(moduleA.Id, activityA, "ACT-A");
        SeedCurriculumActivity(moduleB.Id, activityB, "ACT-B");
        _db.ActivityProgresses.Seed(new ActivityProgress
        {
            Id = Guid.NewGuid(),
            StudentId = _studentId,
            ActivityId = activityA,
            ModuleEnrollmentId = sourceModuleEnrollment.Id,
            ActivityStatus = ActivityStatus.Done,
            IsCompleted = true,
            IsDeleted = false,
        });
        var pending = SeedEnrollment(EnrollmentStatus.Active);
        pending.SourceProgramEnrollmentId = source.Id;
        var newClassId = SeedNewClassSeat(pending.Id);
        SeedSession(newClassId, moduleA.Id, ClassSessionStatus.Completed);
        SeedSession(newClassId, moduleB.Id, ClassSessionStatus.Scheduled);
        var sut = CreateSut();

        await sut.ApplyRebuyCreditsAsync(pending);

        Assert.Equal(50m, pending.ProgressPercent);
        Assert.Equal(EnrollmentStatus.Active, pending.Status);
    }

    [Fact]
    public void SessionAlreadyTaught_UsesCompletedStatusOrPastEndTime()
    {
        var now = _now;
        var completed = new ClassSession
        {
            Status = ClassSessionStatus.Completed,
            StartTime = now.AddDays(1),
            EndTime = now.AddDays(1).AddHours(2),
        };
        var pastScheduled = new ClassSession
        {
            Status = ClassSessionStatus.Scheduled,
            StartTime = now.AddHours(-3),
            EndTime = now.AddHours(-1),
        };
        var futureScheduled = new ClassSession
        {
            Status = ClassSessionStatus.Scheduled,
            StartTime = now.AddDays(2),
            EndTime = now.AddDays(2).AddHours(2),
        };
        var live = new ClassSession
        {
            Status = ClassSessionStatus.InProgress,
            StartTime = now.AddHours(-1),
            EndTime = now.AddHours(1),
        };
        var cancelled = new ClassSession
        {
            Status = ClassSessionStatus.Cancelled,
            StartTime = now.AddHours(-3),
            EndTime = now.AddHours(-1),
        };

        Assert.True(ProgramPurchaseLifecycle.SessionAlreadyTaught(completed, now));
        Assert.True(ProgramPurchaseLifecycle.SessionAlreadyTaught(pastScheduled, now));
        Assert.False(ProgramPurchaseLifecycle.SessionAlreadyTaught(futureScheduled, now));
        Assert.False(ProgramPurchaseLifecycle.SessionAlreadyTaught(live, now));
        Assert.False(ProgramPurchaseLifecycle.SessionAlreadyTaught(cancelled, now));

        var closedWindow = new ClassSession
        {
            SessionKind = SessionKind.AssignmentWindow,
            Status = ClassSessionStatus.Completed,
            StartTime = now.AddDays(-3),
            EndTime = now.AddDays(-1),
        };
        Assert.False(ProgramPurchaseLifecycle.IsTeachingSession(closedWindow));
        Assert.False(ProgramPurchaseLifecycle.SessionAlreadyTaught(closedWindow, now));
    }

    [Fact]
    public void ResolveModuleProgress_UsesFurthestNonCancelledSession()
    {
        var moduleId = Guid.NewGuid();
        var sessions = new[]
        {
            new ClassSession { ModuleId = moduleId, Status = ClassSessionStatus.Scheduled, IsDeleted = false },
            new ClassSession { ModuleId = moduleId, Status = ClassSessionStatus.InProgress, IsDeleted = false },
            new ClassSession { ModuleId = moduleId, Status = ClassSessionStatus.Completed, IsDeleted = true },
            new ClassSession { ModuleId = moduleId, Status = ClassSessionStatus.Cancelled, IsDeleted = false },
        };

        Assert.Equal(
            ClassModuleProgressStatus.InProgress,
            ProgramPurchaseLifecycle.ResolveModuleProgress(sessions));
    }

    [Fact]
    public void ClassBlocksRebuy_True_WhenLaterModuleCompleted()
    {
        var stop = new Module { Id = Guid.NewGuid(), ModuleOrder = 2, IsDeleted = false };
        var later = new Module { Id = Guid.NewGuid(), ModuleOrder = 3, IsDeleted = false };
        var sessions = new[]
        {
            new ClassSession { ModuleId = later.Id, Status = ClassSessionStatus.Completed, IsDeleted = false },
        };

        Assert.True(ProgramPurchaseLifecycle.ClassBlocksRebuy([stop, later], sessions, stop.ModuleOrder));
    }

    [Fact]
    public void ClassBlocksRebuy_False_WhenOnlyEarlierModuleStarted()
    {
        var earlier = new Module { Id = Guid.NewGuid(), ModuleOrder = 1, IsDeleted = false };
        var stop = new Module { Id = Guid.NewGuid(), ModuleOrder = 2, IsDeleted = false };
        var sessions = new[]
        {
            new ClassSession { ModuleId = earlier.Id, Status = ClassSessionStatus.Completed, IsDeleted = false },
        };

        Assert.False(ProgramPurchaseLifecycle.ClassBlocksRebuy([earlier, stop], sessions, stop.ModuleOrder));
    }

    [Fact]
    public void ClassBlocksRebuy_False_WhenOnlyAssignmentWindowIsInProgressOnStopModule()
    {
        var stop = new Module { Id = Guid.NewGuid(), ModuleOrder = 2, IsDeleted = false };
        var sessions = new[]
        {
            new ClassSession
            {
                ModuleId = stop.Id,
                SessionKind = SessionKind.AssignmentWindow,
                Status = ClassSessionStatus.InProgress,
                IsDeleted = false,
            },
            new ClassSession
            {
                ModuleId = stop.Id,
                SessionKind = SessionKind.LiveOnline,
                Status = ClassSessionStatus.Scheduled,
                IsDeleted = false,
            },
        };

        Assert.Equal(
            ClassModuleProgressStatus.NotStarted,
            ProgramPurchaseLifecycle.ResolveModuleProgress(sessions));
        Assert.False(ProgramPurchaseLifecycle.ClassBlocksRebuy([stop], sessions, stop.ModuleOrder));
    }

    [Fact]
    public void ResolveCreditHint_IgnoresOpenAssignmentWindowWhenLivesAreTaught()
    {
        var taughtLive = new ClassSession
        {
            SessionKind = SessionKind.LiveOnline,
            Status = ClassSessionStatus.Completed,
            EndTime = _now.AddDays(-1),
            IsDeleted = false,
        };
        var openWindow = new ClassSession
        {
            SessionKind = SessionKind.AssignmentWindow,
            Status = ClassSessionStatus.InProgress,
            StartTime = _now.AddDays(-1),
            EndTime = _now.AddDays(6),
            IsDeleted = false,
        };

        Assert.Equal(
            RebuyModuleCreditHint.Copied,
            ProgramPurchaseLifecycle.ResolveCreditHint(true, [taughtLive, openWindow], _now));
    }

    // ── Reopen vs concurrent rebuy ────────────────────────────────────────────

    private void SeedClosedAcademicFailForReopen()
    {
        SeedAcademicContext();
        var enrollment = _db.ProgramEnrollments.Items.Single(pe => pe.Id == _enrollmentId);
        enrollment.Status = EnrollmentStatus.Failed;
        enrollment.EndReason = ProgramPurchaseEndReason.AcademicFail;
        enrollment.EndedModuleId = _moduleId;
        enrollment.EndedAt = _now;
        _db.ModuleEnrollments.Items.Single(me => me.Id == _moduleEnrollmentId).Status = EnrollmentStatus.Failed;
        _db.Submissions.Seed(new Submission
        {
            Id = Guid.NewGuid(),
            Code = "SUB-PASS",
            AssignmentId = _assignmentId,
            StudentId = _studentId,
            ModuleEnrollmentId = _moduleEnrollmentId,
            AttemptNumber = 1,
            Status = SubmissionStatus.Graded,
            AssignedGrade = 80,
            IsDeleted = false,
        });
    }

    private void SeedOpenSiblingPurchase(EnrollmentStatus status)
    {
        _db.ProgramEnrollments.Seed(new ProgramEnrollment
        {
            Id = Guid.NewGuid(),
            StudentId = _studentId,
            ProgramId = _programId,
            Status = status,
            IsDeleted = false,
        });
    }

    [Fact]
    public async Task TryReopenAfterGradeCorrectionAsync_Throws_WhenPendingRebuyExists()
    {
        SeedClosedAcademicFailForReopen();
        SeedOpenSiblingPurchase(EnrollmentStatus.PendingPayment);
        var submission = _db.Submissions.Items.Single();
        var assignment = _db.Assignments.Items.Single(a => a.Id == _assignmentId);
        var sut = CreateSut();

        var ex = await Assert.ThrowsAsync<ConflictException>(() =>
            sut.TryReopenAfterGradeCorrectionAsync(submission, assignment));

        Assert.Equal(ProgramPurchaseLifecycle.ReopenBlockedByOpenPurchaseMessage, ex.Message);
        Assert.Equal(
            EnrollmentStatus.Failed,
            _db.ProgramEnrollments.Items.Single(pe => pe.Id == _enrollmentId).Status);
    }

    [Fact]
    public async Task TryReopenAfterGradeCorrectionAsync_Throws_WhenActiveRebuyExists()
    {
        SeedClosedAcademicFailForReopen();
        SeedOpenSiblingPurchase(EnrollmentStatus.Active);
        var submission = _db.Submissions.Items.Single();
        var assignment = _db.Assignments.Items.Single(a => a.Id == _assignmentId);
        var sut = CreateSut();

        await Assert.ThrowsAsync<ConflictException>(() =>
            sut.TryReopenAfterGradeCorrectionAsync(submission, assignment));

        Assert.Equal(
            EnrollmentStatus.Failed,
            _db.ProgramEnrollments.Items.Single(pe => pe.Id == _enrollmentId).Status);
    }

    [Fact]
    public async Task TryReopenAfterGradeCorrectionAsync_Reopens_WhenNoOpenSiblingPurchase()
    {
        SeedClosedAcademicFailForReopen();
        var submission = _db.Submissions.Items.Single();
        var assignment = _db.Assignments.Items.Single(a => a.Id == _assignmentId);
        var sut = CreateSut();

        var reopened = await sut.TryReopenAfterGradeCorrectionAsync(submission, assignment);

        Assert.True(reopened);
        var enrollment = _db.ProgramEnrollments.Items.Single(pe => pe.Id == _enrollmentId);
        Assert.NotEqual(EnrollmentStatus.Failed, enrollment.Status);
        Assert.Null(enrollment.EndReason);
        Assert.Null(enrollment.EndedModuleId);
        Assert.Null(enrollment.EndedAt);
    }

    [Fact]
    public async Task TryReopenAfterAttendanceCorrectionAsync_Throws_WhenPendingRebuyExists()
    {
        SeedAcademicContext();
        var enrollment = _db.ProgramEnrollments.Items.Single(pe => pe.Id == _enrollmentId);
        enrollment.Status = EnrollmentStatus.Failed;
        enrollment.EndReason = ProgramPurchaseEndReason.Attendance;
        enrollment.EndedModuleId = _moduleId;
        enrollment.EndedAt = _now;
        _db.ModuleEnrollments.Items.Single(me => me.Id == _moduleEnrollmentId).Status = EnrollmentStatus.Failed;
        _db.ClassSessions.Seed(new ClassSession
        {
            Id = Guid.NewGuid(),
            ClassId = Guid.NewGuid(),
            ModuleId = _moduleId,
            ActivityId = Guid.NewGuid(),
            Status = ClassSessionStatus.Completed,
            IsDeleted = false,
        });
        SeedOpenSiblingPurchase(EnrollmentStatus.PendingPayment);
        var sut = CreateSut();

        var ex = await Assert.ThrowsAsync<ConflictException>(() =>
            sut.TryReopenAfterAttendanceCorrectionAsync(enrollment, _moduleId));

        Assert.Equal(ProgramPurchaseLifecycle.ReopenBlockedByOpenPurchaseMessage, ex.Message);
        Assert.Equal(EnrollmentStatus.Failed, enrollment.Status);
    }
}
