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
        _db.ClassSessions.Seed(new ClassSession
        {
            Id = Guid.NewGuid(),
            ClassId = classId,
            ModuleId = moduleId,
            Title = "Session",
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
        var sut = CreateSut();

        await sut.ApplyRebuyCreditsAsync(pending);

        var copied = Assert.Single(_db.ModuleEnrollments.Items, me => me.Id != sourceModuleEnrollment.Id);
        Assert.Equal(pending.Id, copied.ProgramEnrollmentId);
        Assert.Equal(EnrollmentStatus.Completed, copied.Status);
        Assert.Equal(100m, copied.ProgressPercent);
        Assert.Equal(8m, copied.FinalGrade);
        Assert.Equal(2, copied.AttemptNumber);
        Assert.Equal(_now, copied.CompletedAt);

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
    public async Task ApplyRebuyCredits_SkipsNonCompletedModules()
    {
        var source = SeedClosedSource(EnrollmentStatus.Failed, endedAt: _now.AddDays(-5));
        var moduleA = SeedModule("MOD-A", 1);
        var moduleB = SeedModule("MOD-B", 2);
        SeedCompletedSourceModuleEnrollment(source.Id, moduleA.Id);
        SeedModuleEnrollment(source.Id, moduleB.Id, EnrollmentStatus.Failed);
        var pending = SeedEnrollment(EnrollmentStatus.Active);
        pending.SourceProgramEnrollmentId = source.Id;
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
        var sut = CreateSut();

        await sut.ApplyRebuyCreditsAsync(pending);

        Assert.Equal(
            2,
            _db.ModuleEnrollments.Items.Count(me => me.ModuleId == module.Id));
    }
}
