using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OboxSteam.Application.Interfaces;
using OboxSteam.Application.Notifications;
using OboxSteam.Application.Realtime;
using OboxSteam.Application.Services;
using OboxSteam.Domain.Entities;
using OboxSteam.Domain.Enums;
using OboxSteam.Test.Helpers;

namespace OboxSteam.Test.UnitTests;

public sealed class SyncEventPublisherTests
{
    private readonly Guid _programId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private readonly Guid _studentId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private readonly Guid _parentId = Guid.Parse("15151515-1515-1515-1515-151515151515");
    private readonly Guid _mentorId = Guid.Parse("14141414-1414-1414-1414-141414141414");

    private readonly DateTime _now = new(2026, 8, 22, 9, 0, 0, DateTimeKind.Utc);

    private readonly InMemoryUnitOfWork _db = new();
    private readonly Mock<ISignalRSyncDispatcher> _dispatcher = new();
    private readonly Mock<ICurrentTime> _currentTime = new();

    private SyncEventPublisher CreateSut()
    {
        _currentTime.Setup(t => t.GetCurrentTime()).Returns(_now);

        return new SyncEventPublisher(
            new NotificationRecipientResolver(_db),
            _dispatcher.Object,
            _currentTime.Object,
            NullLogger<SyncEventPublisher>.Instance);
    }

    [Fact]
    public async Task Publish_ResolvesProgramParticipants_AndDispatchesPerUser()
    {
        _db.ProgramEnrollments.Seed(
            new ProgramEnrollment
            {
                Id = Guid.NewGuid(),
                StudentId = _studentId,
                ProgramId = _programId,
                Status = EnrollmentStatus.Active,
                IsDeleted = false,
            },
            new ProgramEnrollment
            {
                Id = Guid.NewGuid(),
                StudentId = Guid.NewGuid(),
                ProgramId = _programId,
                Status = EnrollmentStatus.Dropped,
                IsDeleted = false,
            });
        _db.ParentStudents.Seed(
            new ParentStudent
            {
                Id = Guid.NewGuid(),
                ParentId = _parentId,
                StudentId = _studentId,
                IsVerified = true,
            },
            new ParentStudent
            {
                Id = Guid.NewGuid(),
                ParentId = Guid.NewGuid(),
                StudentId = _studentId,
                IsVerified = false,
            });
        _db.Classes.Seed(new Class
        {
            Id = Guid.NewGuid(),
            Code = "CLS-001",
            Name = "Cohort A",
            ProgramId = _programId,
            MentorId = _mentorId,
            Status = ClassStatus.Open,
            MaxCapacity = 20,
            StartDate = _now,
            EndDate = _now.AddDays(30),
            IsDeleted = false,
        });
        var sut = CreateSut();

        await sut.PublishAsync(
            SyncScopes.CurriculumStructureChanged,
            NotificationAudience.ForProgramParticipants(_programId),
            "Program",
            _programId);

        _dispatcher.Verify(
            d => d.DispatchToUsersAsync(
                It.Is<IReadOnlyCollection<Guid>>(ids =>
                    ids.Count == 3
                    && ids.Contains(_studentId)
                    && ids.Contains(_parentId)
                    && ids.Contains(_mentorId)),
                It.Is<SyncEvent>(e =>
                    e.Scope == SyncScopes.CurriculumStructureChanged
                    && e.EntityType == "Program"
                    && e.EntityId == _programId
                    && e.At == new DateTimeOffset(_now, TimeSpan.Zero)),
                It.IsAny<CancellationToken>()),
            Times.Once);
        _dispatcher.Verify(
            d => d.DispatchToRoleGroupAsync(
                It.IsAny<string>(), It.IsAny<SyncEvent>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Publish_FansOutManagersAudience_ToRoleGroup()
    {
        var sut = CreateSut();

        await sut.PublishAsync(
            SyncScopes.CurriculumStructureChanged,
            NotificationAudience.ForManagers(),
            "Program",
            _programId);

        _dispatcher.Verify(
            d => d.DispatchToRoleGroupAsync(
                RoleType.Manager.ToString(),
                It.Is<SyncEvent>(e => e.Scope == SyncScopes.CurriculumStructureChanged),
                It.IsAny<CancellationToken>()),
            Times.Once);
        _dispatcher.Verify(
            d => d.DispatchToUsersAsync(
                It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<SyncEvent>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Publish_SkipsDispatch_WhenAudienceResolvesToNobody()
    {
        var sut = CreateSut();

        await sut.PublishAsync(
            SyncScopes.CurriculumStructureChanged,
            NotificationAudience.ForProgramParticipants(_programId),
            "Program",
            _programId);

        _dispatcher.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Publish_SwallowsDispatcherFailures()
    {
        _db.ProgramEnrollments.Seed(new ProgramEnrollment
        {
            Id = Guid.NewGuid(),
            StudentId = _studentId,
            ProgramId = _programId,
            Status = EnrollmentStatus.Active,
            IsDeleted = false,
        });
        _dispatcher
            .Setup(d => d.DispatchToUsersAsync(
                It.IsAny<IReadOnlyCollection<Guid>>(),
                It.IsAny<SyncEvent>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("hub down"));
        var sut = CreateSut();

        // A failed push must never break the request that triggered it.
        await sut.PublishAsync(
            SyncScopes.CurriculumStructureChanged,
            NotificationAudience.ForProgramParticipants(_programId),
            "Program",
            _programId);
    }
}
