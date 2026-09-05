using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OboxSteam.Application.Exceptions;
using OboxSteam.Application.Interfaces;
using OboxSteam.Application.Notifications;
using OboxSteam.Application.Services;
using OboxSteam.Application.Validation;
using OboxSteam.Domain.Entities;
using OboxSteam.Domain.Enums;
using OboxSteam.Test.Helpers;

namespace OboxSteam.Test.UnitTests;

public sealed class RebuyClassCatalogServiceTests
{
    private readonly Guid _studentId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private readonly Guid _programId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private readonly Guid _theoryId = Guid.Parse("33333333-3333-3333-3333-333333333331");
    private readonly Guid _labId = Guid.Parse("33333333-3333-3333-3333-333333333332");
    private readonly Guid _researchId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private readonly Guid _sourcePeId = Guid.Parse("66666666-6666-6666-6666-666666666666");
    private readonly Guid _eligibleId = Guid.Parse("44444444-4444-4444-4444-444444444441");
    private readonly Guid _blockedId = Guid.Parse("44444444-4444-4444-4444-444444444442");
    private readonly Guid _freshId = Guid.Parse("44444444-4444-4444-4444-444444444443");
    private readonly Guid _currentId = Guid.Parse("44444444-4444-4444-4444-444444444444");
    private readonly Guid _mentorId = Guid.Parse("14141414-1414-1414-1414-141414141414");

    private readonly DateTime _now = new(2026, 8, 30, 10, 0, 0, DateTimeKind.Utc);

    private readonly InMemoryUnitOfWork _db = new();
    private readonly Mock<IClaimsService> _claimsService = new();
    private readonly Mock<ICurrentTime> _currentTime = new();
    private readonly Mock<INotificationPublisher> _notificationPublisher = new();

    private RebuyClassCatalogService CreateSut()
    {
        _claimsService.Setup(c => c.GetCurrentUserId).Returns(_studentId);
        _currentTime.Setup(t => t.GetCurrentTime()).Returns(_now);
        _notificationPublisher
            .Setup(n => n.PublishAsync(It.IsAny<NotificationCommand>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var lifecycle = new ProgramPurchaseLifecycle(
            _db,
            _currentTime.Object,
            _notificationPublisher.Object,
            NullLogger<ProgramPurchaseLifecycle>.Instance);

        return new RebuyClassCatalogService(
            _db,
            _claimsService.Object,
            lifecycle,
            new ClassContinuityCatalogBuilder(_db),
            _currentTime.Object,
            NullLogger<RebuyClassCatalogService>.Instance);
    }

    private void SeedStudentAndProgram()
    {
        _db.Users.Seed(new User
        {
            Id = _studentId,
            Code = "STD-026",
            Email = "student26@test.com",
            FullName = "Minh Tran",
            Role = RoleType.Student,
            IsDeleted = false,
        });
        _db.Users.Seed(new User
        {
            Id = _mentorId,
            Code = "MNT-007",
            Email = "mentor7@test.com",
            FullName = "Rebuy Mentor",
            Role = RoleType.Mentor,
            IsDeleted = false,
        });
        _db.Programs.Seed(new Program
        {
            Id = _programId,
            Code = "PRG-FAILREBUY",
            Name = "STEAM Foundations",
            Status = ProgramStatus.Active,
            Price = 1_000_000m,
            RetakeFee = 600_000m,
            Category = ProgramCategory.Technology,
            Level = DifficultyLevel.Beginner,
            IsDeleted = false,
        });
        SeedModule(_theoryId, "MOD-TH", "Foundations", 1, ModuleType.Theory);
        SeedModule(_labId, "MOD-LAB", "Studio Lab", 2, ModuleType.Experiential);
        SeedModule(_researchId, "MOD-RS", "Capstone", 3, ModuleType.Research);
    }

    private void SeedModule(Guid id, string code, string name, int order, ModuleType type)
    {
        _db.Modules.Seed(new Module
        {
            Id = id,
            Code = code,
            Name = name,
            ProgramId = _programId,
            ModuleOrder = order,
            ModuleType = type,
            IsDeleted = false,
        });
    }

    private void SeedFailedSource(DateTime? endedAt = null)
    {
        _db.ProgramEnrollments.Seed(new ProgramEnrollment
        {
            Id = _sourcePeId,
            StudentId = _studentId,
            ProgramId = _programId,
            Status = EnrollmentStatus.Failed,
            EndReason = ProgramPurchaseEndReason.Attendance,
            EndedModuleId = _labId,
            EndedAt = endedAt ?? _now.AddDays(-1),
            IsDeleted = false,
        });
    }

    private Class SeedOpenClass(Guid id, string code, string name)
    {
        var entity = new Class
        {
            Id = id,
            Code = code,
            Name = name,
            ProgramId = _programId,
            MentorId = _mentorId,
            Status = ClassStatus.Open,
            Kind = ClassKind.Standard,
            MaxCapacity = 20,
            StartDate = _now.AddDays(7),
            EndDate = _now.AddDays(63),
            ScheduleSummary = "Weekday lab",
            IsDeleted = false,
        };
        _db.Classes.Seed(entity);
        return entity;
    }

    private void SeedSession(Guid classId, Guid moduleId, ClassSessionStatus status)
    {
        _db.ClassSessions.Seed(new ClassSession
        {
            Id = Guid.NewGuid(),
            ClassId = classId,
            ModuleId = moduleId,
            Title = status.ToString(),
            StartTime = _now,
            EndTime = _now.AddHours(2),
            SessionKind = SessionKind.LiveOnline,
            Status = status,
            IsDeleted = false,
        });
    }

    [Fact]
    public async Task GetRebuyClasses_MarksEligibleAndBlocked_ByStopModuleProgress()
    {
        SeedStudentAndProgram();
        SeedFailedSource();
        _db.ModuleEnrollments.Seed(new ModuleEnrollment
        {
            Id = Guid.NewGuid(),
            StudentId = _studentId,
            ModuleId = _theoryId,
            ProgramEnrollmentId = _sourcePeId,
            Status = EnrollmentStatus.Completed,
            IsDeleted = false,
        });
        SeedOpenClass(_eligibleId, "CLS-ELIGIBLE", "Next Cohort");
        SeedOpenClass(_blockedId, "CLS-BLOCKED", "Mid-lab Cohort");
        SeedOpenClass(_freshId, "CLS-FRESH", "Upcoming Cohort");
        SeedSession(_eligibleId, _theoryId, ClassSessionStatus.Completed);
        SeedSession(_blockedId, _labId, ClassSessionStatus.Completed);
        SeedSession(_freshId, _theoryId, ClassSessionStatus.Scheduled);
        _db.Classes.Seed(new Class
        {
            Id = _currentId,
            Code = "CLS-CURRENT",
            Name = "Current",
            ProgramId = _programId,
            Status = ClassStatus.InProgress,
            Kind = ClassKind.Standard,
            MaxCapacity = 20,
            StartDate = _now.AddDays(-14),
            EndDate = _now.AddDays(42),
            IsDeleted = false,
        });
        SeedSession(_currentId, _theoryId, ClassSessionStatus.Completed);
        SeedSession(_currentId, _labId, ClassSessionStatus.InProgress);

        var result = await CreateSut().GetRebuyClassesAsync(_programId);

        Assert.Equal(_sourcePeId, result.SourceProgramEnrollmentId);
        Assert.Equal(EnrollmentStatus.Failed, result.SourceStatus);
        Assert.True(result.IsRebuy);
        Assert.Equal(ClassContinuityContext.Rebuy, result.Context);
        Assert.Equal(_labId, result.StopModuleId);
        Assert.Equal("MOD-LAB", result.StopModuleCode);
        Assert.True(result.WithinRebuyWindow);
        Assert.Equal(500_000m, result.CheckoutAmount);
        Assert.Equal(4, result.Classes.Count);

        var current = Assert.Single(result.Classes, c => c.ClassId == _currentId);
        Assert.Equal(ClassStatus.InProgress, current.Status);
        Assert.False(current.IsEligible);
        Assert.True(current.Modules.Single(m => m.ModuleId == _labId).BlocksRebuy);

        var eligible = Assert.Single(result.Classes, c => c.ClassId == _eligibleId);
        Assert.True(eligible.IsEligible);
        Assert.Null(eligible.IneligibleReason);
        Assert.Equal("Rebuy Mentor", eligible.MentorName);
        Assert.Equal(ClassModuleProgressStatus.Completed, eligible.Modules.Single(m => m.ModuleId == _theoryId).Progress);
        Assert.False(eligible.Modules.Single(m => m.ModuleId == _theoryId).BlocksRebuy);
        Assert.Equal(RebuyModuleCreditHint.Copied, eligible.Modules.Single(m => m.ModuleId == _theoryId).CreditHint);
        Assert.Equal(ClassModuleProgressStatus.NotStarted, eligible.Modules.Single(m => m.ModuleId == _labId).Progress);
        Assert.Equal(RebuyModuleCreditHint.Ahead, eligible.Modules.Single(m => m.ModuleId == _labId).CreditHint);

        var blocked = Assert.Single(result.Classes, c => c.ClassId == _blockedId);
        Assert.False(blocked.IsEligible);
        Assert.Equal(ProgramPurchaseLifecycle.RebuyClassIneligibleMessage, blocked.IneligibleReason);
        Assert.True(blocked.Modules.Single(m => m.ModuleId == _labId).BlocksRebuy);

        var fresh = Assert.Single(result.Classes, c => c.ClassId == _freshId);
        Assert.True(fresh.IsEligible);
        Assert.Equal(ClassModuleProgressStatus.NotStarted, fresh.Modules.Single(m => m.ModuleId == _theoryId).Progress);
        Assert.Equal(RebuyModuleCreditHint.RedoWithClass, fresh.Modules.Single(m => m.ModuleId == _theoryId).CreditHint);
    }

    [Fact]
    public async Task GetRebuyClasses_MarksSourceClassIneligible()
    {
        SeedStudentAndProgram();
        SeedFailedSource();
        SeedOpenClass(_eligibleId, "CLS-ELIGIBLE", "Next Cohort");
        SeedOpenClass(_currentId, "CLS-OLD", "Previous Class");
        _db.ClassEnrollments.Seed(new ClassEnrollment
        {
            Id = Guid.NewGuid(),
            ClassId = _currentId,
            StudentId = _studentId,
            ProgramEnrollmentId = _sourcePeId,
            Status = ClassEnrollmentStatus.Withdrawn,
            IsDeleted = false,
        });

        var result = await CreateSut().GetRebuyClassesAsync(_programId);

        var oldClass = Assert.Single(result.Classes, c => c.ClassId == _currentId);
        Assert.False(oldClass.IsEligible);
        Assert.Equal(ProgramPurchaseLifecycle.RebuySameClassMessage, oldClass.IneligibleReason);

        var next = Assert.Single(result.Classes, c => c.ClassId == _eligibleId);
        Assert.True(next.IsEligible);
    }

    [Fact]
    public async Task GetRebuyClasses_InProgressClass_IsEligible_WhenStopModuleNotStarted()
    {
        SeedStudentAndProgram();
        SeedFailedSource();
        var running = SeedOpenClass(_currentId, "CLS-RUNNING", "Running Foundations");
        running.Status = ClassStatus.InProgress;
        SeedSession(_currentId, _theoryId, ClassSessionStatus.InProgress);

        var result = await CreateSut().GetRebuyClassesAsync(_programId);

        var item = Assert.Single(result.Classes);
        Assert.Equal(ClassStatus.InProgress, item.Status);
        Assert.True(item.IsEligible);
        Assert.Equal(ClassModuleProgressStatus.InProgress, item.Modules.Single(m => m.ModuleId == _theoryId).Progress);
        Assert.Equal(ClassModuleProgressStatus.NotStarted, item.Modules.Single(m => m.ModuleId == _labId).Progress);
        Assert.False(item.Modules.Single(m => m.ModuleId == _labId).BlocksRebuy);
    }

    [Fact]
    public async Task GetRebuyClasses_ThrowsConflict_WhenActiveEnrollmentExists()
    {
        SeedStudentAndProgram();
        SeedFailedSource();
        _db.ProgramEnrollments.Seed(new ProgramEnrollment
        {
            Id = Guid.NewGuid(),
            StudentId = _studentId,
            ProgramId = _programId,
            Status = EnrollmentStatus.Active,
            IsDeleted = false,
        });

        var ex = await Assert.ThrowsAsync<ConflictException>(
            () => CreateSut().GetRebuyClassesAsync(_programId));
        Assert.Contains("already enrolled", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GetRebuyClasses_NoEnrollment_ReturnsOpenClassesOnly()
    {
        SeedStudentAndProgram();
        SeedOpenClass(_freshId, "CLS-FRESH", "Upcoming Cohort");
        var running = SeedOpenClass(_currentId, "CLS-RUNNING", "Running");
        running.Status = ClassStatus.InProgress;

        var result = await CreateSut().GetRebuyClassesAsync(_programId);

        Assert.False(result.IsRebuy);
        Assert.Null(result.SourceProgramEnrollmentId);
        Assert.Null(result.SourceStatus);
        Assert.Null(result.StopModuleId);
        Assert.False(result.WithinRebuyWindow);
        Assert.Equal(1_000_000m, result.CheckoutAmount);
        var item = Assert.Single(result.Classes);
        Assert.Equal(_freshId, item.ClassId);
        Assert.True(item.IsEligible);
        Assert.All(item.Modules, m => Assert.Equal(RebuyModuleCreditHint.Ahead, m.CreditHint));
    }

    [Fact]
    public async Task GetRebuyClasses_CompletedSource_OpenClassesOnly_RetakePrice()
    {
        SeedStudentAndProgram();
        _db.ProgramEnrollments.Seed(new ProgramEnrollment
        {
            Id = _sourcePeId,
            StudentId = _studentId,
            ProgramId = _programId,
            Status = EnrollmentStatus.Completed,
            CompletedAt = _now.AddDays(-1),
            IsDeleted = false,
        });
        SeedOpenClass(_blockedId, "CLS-BLOCKED", "Mid-lab");
        SeedSession(_blockedId, _labId, ClassSessionStatus.Completed);
        var running = SeedOpenClass(_currentId, "CLS-RUNNING", "Running");
        running.Status = ClassStatus.InProgress;

        var result = await CreateSut().GetRebuyClassesAsync(_programId);

        Assert.False(result.IsRebuy);
        Assert.Equal(_sourcePeId, result.SourceProgramEnrollmentId);
        Assert.Equal(EnrollmentStatus.Completed, result.SourceStatus);
        Assert.Null(result.StopModuleId);
        Assert.True(result.WithinRebuyWindow);
        Assert.Equal(500_000m, result.CheckoutAmount);
        var item = Assert.Single(result.Classes);
        Assert.Equal(_blockedId, item.ClassId);
        Assert.True(item.IsEligible);
        Assert.DoesNotContain(item.Modules, m => m.BlocksRebuy);
    }

    [Fact]
    public async Task GetRebuyClasses_OutsideWindow_ChargesFullPrice()
    {
        SeedStudentAndProgram();
        SeedFailedSource(endedAt: _now.AddDays(-120));
        SeedOpenClass(_freshId, "CLS-FRESH", "Upcoming");

        var result = await CreateSut().GetRebuyClassesAsync(_programId);

        Assert.False(result.WithinRebuyWindow);
        Assert.False(result.IsRebuy);
        Assert.Null(result.StopModuleId);
        Assert.Equal(1_000_000m, result.CheckoutAmount);
        Assert.True(Assert.Single(result.Classes).IsEligible);
    }

    [Fact]
    public async Task GetRebuyClasses_OutsideWindow_OpenClassesOnly()
    {
        SeedStudentAndProgram();
        SeedFailedSource(endedAt: _now.AddDays(-120));
        SeedOpenClass(_freshId, "CLS-FRESH", "Upcoming");
        var running = SeedOpenClass(_currentId, "CLS-RUNNING", "Running");
        running.Status = ClassStatus.InProgress;
        SeedSession(_currentId, _theoryId, ClassSessionStatus.Completed);

        var result = await CreateSut().GetRebuyClassesAsync(_programId);

        Assert.False(result.IsRebuy);
        Assert.False(result.WithinRebuyWindow);
        Assert.Null(result.StopModuleId);
        var item = Assert.Single(result.Classes);
        Assert.Equal(_freshId, item.ClassId);
        Assert.True(item.IsEligible);
    }

    [Fact]
    public async Task GetRebuyClasses_MarksLateJoinIneligible_WhenAssignmentWindowTooSoon()
    {
        SeedStudentAndProgram();
        SeedFailedSource();
        var next = SeedOpenClass(_eligibleId, "CLS-ELIGIBLE", "Next Cohort");
        next.MinHoursBeforeAssignmentJoin = 48;
        _db.ClassSessions.Seed(new ClassSession
        {
            Id = Guid.NewGuid(),
            ClassId = _eligibleId,
            ModuleId = _labId,
            Title = "Lab quiz window",
            SessionKind = SessionKind.AssignmentWindow,
            StartTime = _now.AddDays(-8),
            EndTime = _now.AddDays(1),
            Status = ClassSessionStatus.Scheduled,
            IsDeleted = false,
        });

        var result = await CreateSut().GetRebuyClassesAsync(_programId);

        var item = Assert.Single(result.Classes);
        Assert.False(item.IsEligible);
        Assert.Equal(
            ClassEnrollmentValidator.LateJoinBlockedMessage,
            item.IneligibleReason);
    }

    [Fact]
    public async Task GetRebuyClasses_LateJoin_DoesNotBlock_WhenWindowIsFarEnough()
    {
        SeedStudentAndProgram();
        SeedFailedSource();
        var next = SeedOpenClass(_eligibleId, "CLS-ELIGIBLE", "Next Cohort");
        next.MinHoursBeforeAssignmentJoin = 48;
        _db.ClassSessions.Seed(new ClassSession
        {
            Id = Guid.NewGuid(),
            ClassId = _eligibleId,
            ModuleId = _labId,
            Title = "Lab quiz window",
            SessionKind = SessionKind.AssignmentWindow,
            StartTime = _now.AddHours(72),
            EndTime = _now.AddHours(74),
            Status = ClassSessionStatus.Scheduled,
            IsDeleted = false,
        });

        var result = await CreateSut().GetRebuyClassesAsync(_programId);

        var item = Assert.Single(result.Classes);
        Assert.True(item.IsEligible);
        Assert.Null(item.IneligibleReason);
    }

    [Fact]
    public async Task GetRebuyClasses_DoesNotBlock_WhenStopModuleOnlyHasOpenAssignmentWindow()
    {
        SeedStudentAndProgram();
        SeedFailedSource();
        _db.ModuleEnrollments.Seed(new ModuleEnrollment
        {
            Id = Guid.NewGuid(),
            StudentId = _studentId,
            ModuleId = _theoryId,
            ProgramEnrollmentId = _sourcePeId,
            Status = EnrollmentStatus.Completed,
            IsDeleted = false,
        });
        SeedOpenClass(_eligibleId, "CLS-ELIGIBLE", "Next Cohort");
        SeedSession(_eligibleId, _theoryId, ClassSessionStatus.Completed);
        SeedSession(_eligibleId, _labId, ClassSessionStatus.Scheduled);
        _db.ClassSessions.Seed(new ClassSession
        {
            Id = Guid.NewGuid(),
            ClassId = _eligibleId,
            ModuleId = _labId,
            Title = "Lab work window",
            SessionKind = SessionKind.AssignmentWindow,
            StartTime = _now.AddDays(-1),
            EndTime = _now.AddDays(6),
            Status = ClassSessionStatus.InProgress,
            IsDeleted = false,
        });

        var result = await CreateSut().GetRebuyClassesAsync(_programId);

        var eligible = Assert.Single(result.Classes);
        Assert.True(eligible.IsEligible);
        Assert.Equal(RebuyModuleCreditHint.Copied, eligible.Modules.Single(m => m.ModuleId == _theoryId).CreditHint);
        Assert.Equal(ClassModuleProgressStatus.NotStarted, eligible.Modules.Single(m => m.ModuleId == _labId).Progress);
        Assert.Equal(RebuyModuleCreditHint.Ahead, eligible.Modules.Single(m => m.ModuleId == _labId).CreditHint);
    }

    [Fact]
    public async Task GetRebuyClasses_LateJoin_DoesNotBlock_LiveOnlineSession()
    {
        SeedStudentAndProgram();
        SeedFailedSource();
        var next = SeedOpenClass(_eligibleId, "CLS-ELIGIBLE", "Next Cohort");
        next.MinHoursBeforeAssignmentJoin = 48;
        SeedSession(_eligibleId, _labId, ClassSessionStatus.Scheduled);
        var live = _db.ClassSessions.Items.Single(cs => cs.ClassId == _eligibleId);
        live.StartTime = _now.AddHours(10);
        live.EndTime = _now.AddHours(12);

        var result = await CreateSut().GetRebuyClassesAsync(_programId);

        Assert.True(Assert.Single(result.Classes).IsEligible);
    }

    [Fact]
    public async Task GetRebuyClasses_SourceClassReason_WinsOverLateJoin()
    {
        SeedStudentAndProgram();
        SeedFailedSource();
        var oldClass = SeedOpenClass(_currentId, "CLS-OLD", "Previous Class");
        oldClass.MinHoursBeforeAssignmentJoin = 48;
        _db.ClassEnrollments.Seed(new ClassEnrollment
        {
            Id = Guid.NewGuid(),
            ClassId = _currentId,
            StudentId = _studentId,
            ProgramEnrollmentId = _sourcePeId,
            Status = ClassEnrollmentStatus.Withdrawn,
            IsDeleted = false,
        });
        _db.ClassSessions.Seed(new ClassSession
        {
            Id = Guid.NewGuid(),
            ClassId = _currentId,
            ModuleId = _labId,
            Title = "Quiz window",
            SessionKind = SessionKind.AssignmentWindow,
            StartTime = _now.AddHours(10),
            EndTime = _now.AddHours(12),
            Status = ClassSessionStatus.Scheduled,
            IsDeleted = false,
        });

        var result = await CreateSut().GetRebuyClassesAsync(_programId);

        var item = Assert.Single(result.Classes);
        Assert.False(item.IsEligible);
        Assert.Equal(ProgramPurchaseLifecycle.RebuySameClassMessage, item.IneligibleReason);
    }

    [Fact]
    public async Task GetRebuyClasses_ExcludesFullAndRemedialClasses()
    {
        SeedStudentAndProgram();
        SeedFailedSource();
        SeedOpenClass(_freshId, "CLS-FRESH", "Upcoming");
        var full = SeedOpenClass(Guid.NewGuid(), "CLS-FULL", "Full");
        full.MaxCapacity = 1;
        _db.ClassEnrollments.Seed(new ClassEnrollment
        {
            Id = Guid.NewGuid(),
            ClassId = full.Id,
            StudentId = Guid.NewGuid(),
            ProgramEnrollmentId = Guid.NewGuid(),
            Status = ClassEnrollmentStatus.Active,
            IsDeleted = false,
        });
        var remedial = SeedOpenClass(Guid.NewGuid(), "CLS-REM", "Remedial");
        remedial.Kind = ClassKind.Remedial;

        var result = await CreateSut().GetRebuyClassesAsync(_programId);

        Assert.Equal(_freshId, Assert.Single(result.Classes).ClassId);
    }

    [Fact]
    public async Task GetRebuyClasses_ThrowsNotFound_WhenProgramMissing()
    {
        SeedStudentAndProgram();
        SeedFailedSource();

        await Assert.ThrowsAsync<NotFoundException>(
            () => CreateSut().GetRebuyClassesAsync(Guid.Parse("99999999-9999-9999-9999-999999999999")));
    }
}
