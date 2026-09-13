using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OboxSteam.Application.DTOs.CertificateDTO;
using OboxSteam.Application.Interfaces;
using OboxSteam.Application.Notifications;
using OboxSteam.Application.Services;
using OboxSteam.Domain.Entities;
using OboxSteam.Domain.Enums;
using OboxSteam.Test.Helpers;

namespace OboxSteam.Test.UnitTests;

public sealed class BundleProgressServiceTests
{
    private readonly Guid _studentId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private readonly Guid _programAId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private readonly Guid _programBId = Guid.Parse("23232323-2323-2323-2323-232323232323");
    private readonly Guid _programCId = Guid.Parse("24242424-2424-2424-2424-242424242424");
    private readonly Guid _bundleId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private readonly Guid _peAId = Guid.Parse("44444444-4444-4444-4444-444444444444");
    private readonly Guid _peBId = Guid.Parse("45454545-4545-4545-4545-454545454545");
    private readonly Guid _peCId = Guid.Parse("46464646-4646-4646-4646-464646464646");
    private readonly Guid _bundleEnrollmentId = Guid.Parse("55555555-5555-5555-5555-555555555555");

    private readonly InMemoryUnitOfWork _db = new();
    private readonly Mock<ICertificateService> _certificateService = new();
    private readonly Mock<INotificationPublisher> _notificationPublisher = new();

    private BundleProgressService CreateSut()
    {
        _certificateService
            .Setup(c => c.EnsureBundleCertificateInternalAsync(It.IsAny<Guid>()))
            .ReturnsAsync((CertificateDetailDto?)null);
        _notificationPublisher
            .Setup(n => n.PublishManyAsync(
                It.IsAny<IReadOnlyList<NotificationCommand>>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        return new BundleProgressService(
            _db,
            _certificateService.Object,
            _notificationPublisher.Object,
            NullLogger<BundleProgressService>.Instance);
    }

    private void SeedPathway()
    {
        _db.Users.Seed(new User
        {
            Id = _studentId,
            Code = "STD-001",
            Email = "student@test.com",
            FullName = "Alice",
            Role = RoleType.Student,
        });
        _db.Programs.Seed(
            new Program
            {
                Id = _programAId,
                Code = "PRG-A",
                Name = "Robotics 1",
                Category = ProgramCategory.Technology,
                Status = ProgramStatus.Active,
            },
            new Program
            {
                Id = _programBId,
                Code = "PRG-B",
                Name = "Robotics 2",
                Category = ProgramCategory.Technology,
                Status = ProgramStatus.Active,
            },
            new Program
            {
                Id = _programCId,
                Code = "PRG-C",
                Name = "Robotics 3",
                Category = ProgramCategory.Technology,
                Status = ProgramStatus.Active,
            });
        _db.ProgramBundles.Seed(new ProgramBundle
        {
            Id = _bundleId,
            Code = "BDL-ROB",
            Name = "Robotics pathway",
            Category = ProgramCategory.Technology,
            Price = 2_500_000m,
            Status = ProgramBundleStatus.Active,
        });
        _db.ProgramBundleItems.Seed(
            new ProgramBundleItem
            {
                Id = Guid.NewGuid(),
                BundleId = _bundleId,
                ProgramId = _programAId,
                SortOrder = 1,
            },
            new ProgramBundleItem
            {
                Id = Guid.NewGuid(),
                BundleId = _bundleId,
                ProgramId = _programBId,
                SortOrder = 2,
                RequiresPreviousCompletion = true,
            },
            new ProgramBundleItem
            {
                Id = Guid.NewGuid(),
                BundleId = _bundleId,
                ProgramId = _programCId,
                SortOrder = 3,
                RequiresPreviousCompletion = true,
            });
        _db.BundleEnrollments.Seed(new BundleEnrollment
        {
            Id = _bundleEnrollmentId,
            StudentId = _studentId,
            BundleId = _bundleId,
            Status = BundleEnrollmentStatus.Active,
            ProgressPercent = 0m,
        });
        _db.ProgramEnrollments.Seed(
            new ProgramEnrollment
            {
                Id = _peAId,
                StudentId = _studentId,
                ProgramId = _programAId,
                Status = EnrollmentStatus.Completed,
                ProgressPercent = 100m,
            },
            new ProgramEnrollment
            {
                Id = _peBId,
                StudentId = _studentId,
                ProgramId = _programBId,
                Status = EnrollmentStatus.Active,
                ProgressPercent = 0m,
            },
            new ProgramEnrollment
            {
                Id = _peCId,
                StudentId = _studentId,
                ProgramId = _programCId,
                Status = EnrollmentStatus.Active,
                ProgressPercent = 0m,
            });
    }

    [Fact]
    public async Task Sync_CompletingFirstProgram_UpdatesPercent_AndNotifiesCompletedPlusUnlock()
    {
        SeedPathway();
        var sut = CreateSut();

        await sut.SyncAfterProgramProgressAsync(_peAId, EnrollmentStatus.Active);

        var bundleEnrollment = _db.BundleEnrollments.Items.Single();
        Assert.Equal(BundleEnrollmentStatus.Active, bundleEnrollment.Status);
        Assert.Equal(33.33m, bundleEnrollment.ProgressPercent);

        _notificationPublisher.Verify(
            n => n.PublishManyAsync(
                It.Is<IReadOnlyList<NotificationCommand>>(commands =>
                    commands.Count == 2
                    && commands.Any(c => c.Type == NotificationType.ProgramCompleted
                                         && c.Payload!.ProgramId == _programAId)
                    && commands.Any(c => c.Type == NotificationType.ProgramUnlocked
                                         && c.Payload!.ProgramId == _programBId)),
                It.IsAny<CancellationToken>()),
            Times.Once);
        _certificateService.Verify(
            c => c.EnsureBundleCertificateInternalAsync(It.IsAny<Guid>()),
            Times.Never);
    }

    [Fact]
    public async Task Sync_AlreadyCompleted_DoesNotRepeatNotifications()
    {
        SeedPathway();
        var sut = CreateSut();

        await sut.SyncAfterProgramProgressAsync(_peAId, EnrollmentStatus.Completed);

        _notificationPublisher.Verify(
            n => n.PublishManyAsync(
                It.IsAny<IReadOnlyList<NotificationCommand>>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Sync_CompletingLastProgram_CompletesBundle_AndIssuesPathwayCertificate()
    {
        SeedPathway();
        _db.ProgramEnrollments.Items.Single(pe => pe.Id == _peBId).Status = EnrollmentStatus.Completed;
        _db.ProgramEnrollments.Items.Single(pe => pe.Id == _peBId).ProgressPercent = 100m;
        _db.ProgramEnrollments.Items.Single(pe => pe.Id == _peCId).Status = EnrollmentStatus.Completed;
        _db.ProgramEnrollments.Items.Single(pe => pe.Id == _peCId).ProgressPercent = 100m;
        var sut = CreateSut();

        await sut.SyncAfterProgramProgressAsync(_peCId, EnrollmentStatus.Active);

        var bundleEnrollment = _db.BundleEnrollments.Items.Single();
        Assert.Equal(BundleEnrollmentStatus.Completed, bundleEnrollment.Status);
        Assert.Equal(100m, bundleEnrollment.ProgressPercent);
        _certificateService.Verify(
            c => c.EnsureBundleCertificateInternalAsync(_bundleEnrollmentId),
            Times.Once);
        _notificationPublisher.Verify(
            n => n.PublishManyAsync(
                It.Is<IReadOnlyList<NotificationCommand>>(commands =>
                    commands.Any(c => c.Type == NotificationType.ProgramCompleted)
                    && commands.Any(c => c.Type == NotificationType.BundleCompleted)
                    && commands.All(c => c.Type != NotificationType.ProgramUnlocked)),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Sync_NoBundle_StillNotifiesProgramCompleted()
    {
        _db.Users.Seed(new User
        {
            Id = _studentId,
            Code = "STD-001",
            Email = "student@test.com",
            FullName = "Alice",
            Role = RoleType.Student,
        });
        _db.Programs.Seed(new Program
        {
            Id = _programAId,
            Code = "PRG-A",
            Name = "Solo program",
            Category = ProgramCategory.Technology,
            Status = ProgramStatus.Active,
        });
        _db.ProgramEnrollments.Seed(new ProgramEnrollment
        {
            Id = _peAId,
            StudentId = _studentId,
            ProgramId = _programAId,
            Status = EnrollmentStatus.Completed,
            ProgressPercent = 100m,
        });
        var sut = CreateSut();

        await sut.SyncAfterProgramProgressAsync(_peAId, EnrollmentStatus.Active);

        _notificationPublisher.Verify(
            n => n.PublishManyAsync(
                It.Is<IReadOnlyList<NotificationCommand>>(commands =>
                    commands.Count == 1 && commands[0].Type == NotificationType.ProgramCompleted),
                It.IsAny<CancellationToken>()),
            Times.Once);
        _certificateService.Verify(
            c => c.EnsureBundleCertificateInternalAsync(It.IsAny<Guid>()),
            Times.Never);
    }

    [Fact]
    public async Task Sync_IncludesPriorOwnedProgressInMean()
    {
        SeedPathway();
        _db.ProgramEnrollments.Items.Single(pe => pe.Id == _peAId).ProgressPercent = 100m;
        _db.ProgramEnrollments.Items.Single(pe => pe.Id == _peBId).ProgressPercent = 40m;
        var sut = CreateSut();

        await sut.SyncAfterProgramProgressAsync(_peAId, EnrollmentStatus.Active);

        Assert.Equal(46.67m, _db.BundleEnrollments.Items.Single().ProgressPercent);
    }
}
