using Microsoft.Extensions.Logging.Abstractions;
using Moq;
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
                It.Is<NotificationCommand>(c => c.Type == NotificationType.ModuleFailed),
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
        EnrollmentStatus programStatus = EnrollmentStatus.Active)
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
}
