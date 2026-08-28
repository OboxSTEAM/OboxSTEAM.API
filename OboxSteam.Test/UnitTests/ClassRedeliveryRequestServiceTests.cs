using Microsoft.Extensions.Logging.Abstractions;
using Moq;
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

    private readonly InMemoryUnitOfWork _db = new();
    private readonly Mock<IClaimsService> _claimsService = new();
    private readonly Mock<INotificationPublisher> _notificationPublisher = new();

    private ClassRedeliveryRequestService CreateSut()
    {
        _claimsService.Setup(c => c.GetCurrentUserId).Returns(_studentId);
        _notificationPublisher
            .Setup(n => n.PublishAsync(It.IsAny<NotificationCommand>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        return new ClassRedeliveryRequestService(
            _db,
            _claimsService.Object,
            _notificationPublisher.Object,
            NullLogger<ClassRedeliveryRequestService>.Instance);
    }

    [Fact]
    public async Task GetCandidates_IncludesFullClass_WithZeroSeatsRemaining()
    {
        SeedStudent();
        SeedModule();
        SeedClasses();
        SeedSessions();
        SeedRedeliveryRequest();
        var sut = CreateSut();

        var result = await sut.GetCandidatesAsync(_requestId);

        var full = Assert.Single(result, c => c.ClassId == _fullClassId);
        Assert.Equal(4, full.MaxCapacity);
        Assert.Equal(4, full.SeatsTaken);
        Assert.Equal(0, full.SeatsRemaining);
        Assert.Contains(result, c => c.ClassId == _openClassId && c.SeatsRemaining > 0);
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
            Status = ProgramStatus.Active,
            IsDeleted = false,
        });
        _db.Modules.Seed(new Module
        {
            Id = _moduleId,
            Code = "MOD-WS7-EXP",
            ProgramId = _programId,
            Name = "EXP",
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
                MaxCapacity = 20,
                StartDate = DateTime.UtcNow.AddDays(-30),
                EndDate = DateTime.UtcNow.AddDays(30),
                IsDeleted = false,
            },
            new Class
            {
                Id = _openClassId,
                Code = "CLS-OPEN",
                Name = "Open",
                ProgramId = _programId,
                Status = ClassStatus.Open,
                MaxCapacity = 10,
                StartDate = DateTime.UtcNow.AddDays(14),
                EndDate = DateTime.UtcNow.AddDays(98),
                IsDeleted = false,
            },
            new Class
            {
                Id = _fullClassId,
                Code = "CLS-FULL",
                Name = "Full",
                ProgramId = _programId,
                Status = ClassStatus.Open,
                MaxCapacity = 4,
                StartDate = DateTime.UtcNow.AddDays(21),
                EndDate = DateTime.UtcNow.AddDays(105),
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
            ProgramEnrollmentId = Guid.NewGuid(),
            Kind = ClassEnrollmentKind.Primary,
            Status = ClassEnrollmentStatus.Active,
            IsDeleted = false,
        });
    }

    private void SeedSessions()
    {
        var futureStart = DateTime.UtcNow.AddDays(21);
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
        _db.ModuleEnrollments.Seed(new ModuleEnrollment
        {
            Id = _moduleEnrollmentId,
            StudentId = _studentId,
            ModuleId = _moduleId,
            ProgramEnrollmentId = Guid.NewGuid(),
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
