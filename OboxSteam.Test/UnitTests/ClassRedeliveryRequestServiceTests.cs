using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OboxSteam.Application.DTOs.ClassRedeliveryDTO;
using OboxSteam.Application.Exceptions;
using OboxSteam.Application.Interfaces;
using OboxSteam.Application.Notifications;
using OboxSteam.Application.Services;
using OboxSteam.Domain.Entities;
using OboxSteam.Domain.Enums;
using OboxSteam.Test.Helpers;

namespace OboxSteam.Test.UnitTests;

public sealed class ClassRedeliveryRequestServiceTests
{
    private readonly Guid _studentId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private readonly Guid _programId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private readonly Guid _moduleId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private readonly Guid _sourceClassId = Guid.Parse("44444444-4444-4444-4444-444444444444");
    private readonly Guid _openClassId = Guid.Parse("47474747-4747-4747-4747-474747474747");
    private readonly Guid _fullClassId = Guid.Parse("48484848-4848-4848-4848-484848484848");
    private readonly Guid _requestId = Guid.Parse("49494949-4949-4949-4949-494949494949");
    private readonly Guid _moduleEnrollmentId = Guid.Parse("50505050-5050-5050-5050-505050505050");
    private readonly Guid _programEnrollmentId = Guid.Parse("51515151-5151-5151-5151-515151515151");

    private readonly DateTime _now = new(2026, 8, 30, 10, 0, 0, DateTimeKind.Utc);

    private readonly InMemoryUnitOfWork _db = new();
    private readonly Mock<IClaimsService> _claimsService = new();
    private readonly Mock<ICurrentTime> _currentTime = new();
    private readonly Mock<INotificationPublisher> _notificationPublisher = new();

    private ClassRedeliveryRequestService CreateSut()
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

        var catalogService = new RebuyClassCatalogService(
            _db,
            _claimsService.Object,
            lifecycle,
            new ClassContinuityCatalogBuilder(_db),
            _currentTime.Object,
            NullLogger<RebuyClassCatalogService>.Instance);

        return new ClassRedeliveryRequestService(
            _db,
            _claimsService.Object,
            _notificationPublisher.Object,
            catalogService,
            _currentTime.Object,
            NullLogger<ClassRedeliveryRequestService>.Instance);
    }

    [Fact]
    public async Task GetCandidates_ReturnsContinuityCatalog_WithoutFullClassAndWithoutSourceClass()
    {
        SeedStudent();
        SeedModule();
        SeedClasses();
        SeedSessions();
        SeedRedeliveryRequest();
        var sut = CreateSut();

        var result = await sut.GetCandidatesAsync(_requestId);

        Assert.Equal(ClassContinuityContext.ActiveRedelivery, result.Context);
        Assert.False(result.IsRebuy);
        Assert.Equal(1_000_000m, result.CheckoutAmount);
        Assert.DoesNotContain(result.Classes, c => c.ClassId == _fullClassId);

        var open = Assert.Single(result.Classes, c => c.ClassId == _openClassId);
        Assert.True(open.IsEligible);
        Assert.True(open.SeatsRemaining > 0);
        Assert.NotEmpty(open.ModuleSessions);

        var source = Assert.Single(result.Classes, c => c.ClassId == _sourceClassId);
        Assert.False(source.IsEligible);
        Assert.Equal(ProgramPurchaseLifecycle.RebuySameClassMessage, source.IneligibleReason);
    }

    [Fact]
    public async Task Create_AlwaysSetsAwaitingClassSelection_EvenWithoutEligibleClasses()
    {
        SeedStudent();
        SeedModule();
        // Only source class — catalog may list it as ineligible; no other Standard seats.
        _db.Classes.Seed(new Class
        {
            Id = _sourceClassId,
            Code = "CLS-SOURCE",
            Name = "Source",
            ProgramId = _programId,
            Status = ClassStatus.InProgress,
            Kind = ClassKind.Standard,
            MaxCapacity = 20,
            StartDate = _now.AddDays(-30),
            EndDate = _now.AddDays(30),
            IsDeleted = false,
        });
        _db.ProgramEnrollments.Seed(new ProgramEnrollment
        {
            Id = _programEnrollmentId,
            StudentId = _studentId,
            ProgramId = _programId,
            Status = EnrollmentStatus.Active,
            IsDeleted = false,
        });
        _db.ModuleEnrollments.Seed(new ModuleEnrollment
        {
            Id = _moduleEnrollmentId,
            StudentId = _studentId,
            ModuleId = _moduleId,
            ProgramEnrollmentId = _programEnrollmentId,
            Status = EnrollmentStatus.Failed,
            AttemptNumber = 1,
            IsDeleted = false,
        });
        _db.ClassEnrollments.Seed(new ClassEnrollment
        {
            Id = Guid.NewGuid(),
            ClassId = _sourceClassId,
            StudentId = _studentId,
            ProgramEnrollmentId = _programEnrollmentId,
            Kind = ClassEnrollmentKind.Primary,
            Status = ClassEnrollmentStatus.Active,
            IsDeleted = false,
        });

        var result = await CreateSut().CreateAsync(new CreateClassRedeliveryRequestDto
        {
            ModuleEnrollmentId = _moduleEnrollmentId,
        });

        Assert.Equal(ClassRedeliveryRequestStatus.AwaitingClassSelection, result.Status);
        Assert.DoesNotContain(
            _db.ClassRedeliveryRequests.Items,
            r => r.Status == ClassRedeliveryRequestStatus.PendingManager);
    }

    [Fact]
    public async Task ManagerAndIntensiveMethods_ThrowGone()
    {
        var sut = CreateSut();

        await Assert.ThrowsAsync<OboxSteam.Application.Exceptions.GoneException>(() =>
            sut.GetPendingManagerAsync());
        await Assert.ThrowsAsync<OboxSteam.Application.Exceptions.GoneException>(() =>
            sut.GetWaitlistGroupedAsync());
        await Assert.ThrowsAsync<OboxSteam.Application.Exceptions.GoneException>(() =>
            sut.AcceptIntensiveAsync(Guid.NewGuid()));
        await Assert.ThrowsAsync<OboxSteam.Application.Exceptions.GoneException>(() =>
            sut.DeclineIntensiveAsync(Guid.NewGuid()));
        await Assert.ThrowsAsync<OboxSteam.Application.Exceptions.GoneException>(() =>
            sut.OpenRemedialClassAsync(new OpenRemedialClassRequestDto
            {
                ModuleId = _moduleId,
                MentorId = Guid.NewGuid(),
                StartDate = _now,
            }));
        await Assert.ThrowsAsync<OboxSteam.Application.Exceptions.GoneException>(() =>
            sut.ManagerAssignTargetAsync(_requestId, new DecideClassRedeliveryRequestDto
            {
                TargetClassId = _openClassId,
            }));
        await Assert.ThrowsAsync<OboxSteam.Application.Exceptions.GoneException>(() =>
            sut.RejectAsync(_requestId, null));
    }

    private void SeedStudent()
    {
        _db.Users.Seed(new User
        {
            Id = _studentId,
            Code = "STD-WS7-B",
            Email = "b@test.com",
            Role = RoleType.Student,
            IsDeleted = false,
        });
    }

    private void SeedModule()
    {
        _db.Programs.Seed(new Program
        {
            Id = _programId,
            Code = "PRG-WS7",
            Name = "WS7",
            Category = ProgramCategory.Technology,
            Level = DifficultyLevel.Beginner,
            Price = 2_000_000m,
            RetakeFee = 1_200_000m,
            Status = ProgramStatus.Active,
            IsDeleted = false,
        });
        _db.Modules.Seed(new Module
        {
            Id = _moduleId,
            Code = "MOD-WS7-EXP",
            ProgramId = _programId,
            Name = "EXP",
            ModuleOrder = 1,
            ModuleType = ModuleType.Experiential,
            IsDeleted = false,
        });
    }

    private void SeedClasses()
    {
        _db.Classes.Seed(
            new Class
            {
                Id = _sourceClassId,
                Code = "CLS-SOURCE",
                Name = "Source",
                ProgramId = _programId,
                Status = ClassStatus.InProgress,
                Kind = ClassKind.Standard,
                MaxCapacity = 20,
                StartDate = _now.AddDays(-30),
                EndDate = _now.AddDays(30),
                IsDeleted = false,
            },
            new Class
            {
                Id = _openClassId,
                Code = "CLS-OPEN",
                Name = "Open",
                ProgramId = _programId,
                Status = ClassStatus.Open,
                Kind = ClassKind.Standard,
                MaxCapacity = 10,
                StartDate = _now.AddDays(14),
                EndDate = _now.AddDays(98),
                IsDeleted = false,
            },
            new Class
            {
                Id = _fullClassId,
                Code = "CLS-FULL",
                Name = "Full",
                ProgramId = _programId,
                Status = ClassStatus.Open,
                Kind = ClassKind.Standard,
                MaxCapacity = 4,
                StartDate = _now.AddDays(21),
                EndDate = _now.AddDays(105),
                IsDeleted = false,
            });

        for (var i = 0; i < 4; i++)
        {
            _db.ClassEnrollments.Seed(new ClassEnrollment
            {
                Id = Guid.NewGuid(),
                ClassId = _fullClassId,
                StudentId = Guid.NewGuid(),
                ProgramEnrollmentId = Guid.NewGuid(),
                Status = ClassEnrollmentStatus.Active,
                IsDeleted = false,
            });
        }

        _db.ClassEnrollments.Seed(new ClassEnrollment
        {
            Id = Guid.NewGuid(),
            ClassId = _sourceClassId,
            StudentId = _studentId,
            ProgramEnrollmentId = _programEnrollmentId,
            Kind = ClassEnrollmentKind.Primary,
            Status = ClassEnrollmentStatus.Active,
            IsDeleted = false,
        });
    }

    private void SeedSessions()
    {
        var futureStart = _now.AddDays(21);
        foreach (var classId in new[] { _openClassId, _fullClassId })
        {
            _db.ClassSessions.Seed(new ClassSession
            {
                Id = Guid.NewGuid(),
                ClassId = classId,
                ModuleId = _moduleId,
                Title = "EXP future",
                StartTime = futureStart,
                EndTime = futureStart.AddHours(2),
                SessionKind = SessionKind.LiveOnline,
                Status = ClassSessionStatus.Scheduled,
                IsDeleted = false,
            });
        }
    }

    private void SeedRedeliveryRequest()
    {
        _db.ProgramEnrollments.Seed(new ProgramEnrollment
        {
            Id = _programEnrollmentId,
            StudentId = _studentId,
            ProgramId = _programId,
            Status = EnrollmentStatus.Active,
            IsDeleted = false,
        });

        _db.ModuleEnrollments.Seed(new ModuleEnrollment
        {
            Id = _moduleEnrollmentId,
            StudentId = _studentId,
            ModuleId = _moduleId,
            ProgramEnrollmentId = _programEnrollmentId,
            Status = EnrollmentStatus.Active,
            IsDeleted = false,
        });

        _db.ClassRedeliveryRequests.Seed(new ClassRedeliveryRequest
        {
            Id = _requestId,
            StudentId = _studentId,
            ModuleEnrollmentId = _moduleEnrollmentId,
            ModuleId = _moduleId,
            SourceClassId = _sourceClassId,
            RequestedByUserId = _studentId,
            Status = ClassRedeliveryRequestStatus.AwaitingClassSelection,
            IsDeleted = false,
        });
    }
}
