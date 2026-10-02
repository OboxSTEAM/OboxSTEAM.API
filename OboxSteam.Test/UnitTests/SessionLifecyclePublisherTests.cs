using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OboxSteam.Application.Interfaces;
using OboxSteam.Application.Notifications;
using OboxSteam.Application.Services;
using OboxSteam.Domain.Entities;
using OboxSteam.Domain.Enums;
using OboxSteam.Test.Helpers;

namespace OboxSteam.Test.UnitTests;

public sealed class SessionLifecyclePublisherTests
{
    private readonly Guid _classId = Guid.Parse("44444444-4444-4444-4444-444444444444");
    private readonly Guid _sessionId = Guid.Parse("55555555-5555-5555-5555-555555555555");
    private readonly Guid _moduleId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private readonly Guid _programId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private readonly DateTime _now = new(2026, 10, 1, 11, 0, 0, DateTimeKind.Utc);

    private readonly InMemoryUnitOfWork _db = new();
    private readonly Mock<ICurrentTime> _currentTime = new();
    private readonly Mock<INotificationPublisher> _notifications = new();

    private SessionLifecyclePublisher CreateSut()
    {
        _currentTime.Setup(t => t.GetCurrentTime()).Returns(_now);
        _notifications
            .Setup(n => n.PublishManyAsync(It.IsAny<IReadOnlyList<NotificationCommand>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        return new SessionLifecyclePublisher(
            _db,
            _currentTime.Object,
            _notifications.Object,
            NullLogger<SessionLifecyclePublisher>.Instance);
    }

    [Theory]
    [InlineData(ClassSessionStatus.InProgress, SessionKind.Offline)]
    [InlineData(ClassSessionStatus.Scheduled, SessionKind.LiveOnline)]
    public async Task CompleteElapsed_MovesToCompleted_AndPublishesOnce(
        ClassSessionStatus status,
        SessionKind kind)
    {
        SeedClass();
        SeedSession(status, kind, endTime: _now.AddMinutes(-1));
        var sut = CreateSut();

        var first = await sut.CompleteElapsedSessionsAsync();
        var second = await sut.CompleteElapsedSessionsAsync();

        Assert.Equal(1, first);
        Assert.Equal(0, second);
        Assert.Equal(ClassSessionStatus.Completed, _db.ClassSessions.GetQueryable().Single().Status);
        _notifications.Verify(
            n => n.PublishManyAsync(
                It.Is<IReadOnlyList<NotificationCommand>>(cmds =>
                    cmds.Count == 1
                    && cmds[0].Type == NotificationType.ClassSessionCompleted
                    && cmds[0].EntityId == _sessionId),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task CompleteElapsed_CompletesAtExactEndTime()
    {
        SeedClass();
        SeedSession(ClassSessionStatus.InProgress, SessionKind.Offline, endTime: _now);
        var sut = CreateSut();

        Assert.Equal(1, await sut.CompleteElapsedSessionsAsync());
    }

    [Fact]
    public async Task CompleteElapsed_Skips_WhenEndTimeInFuture()
    {
        SeedClass();
        SeedSession(ClassSessionStatus.InProgress, SessionKind.Offline, endTime: _now.AddMinutes(1));
        var sut = CreateSut();

        Assert.Equal(0, await sut.CompleteElapsedSessionsAsync());
        Assert.Equal(ClassSessionStatus.InProgress, _db.ClassSessions.GetQueryable().Single().Status);
        _notifications.Verify(
            n => n.PublishManyAsync(It.IsAny<IReadOnlyList<NotificationCommand>>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task CompleteElapsed_Skips_AssignmentWindow()
    {
        SeedClass();
        SeedSession(ClassSessionStatus.Scheduled, SessionKind.AssignmentWindow, endTime: _now.AddHours(-1));
        var sut = CreateSut();

        Assert.Equal(0, await sut.CompleteElapsedSessionsAsync());
        Assert.Equal(ClassSessionStatus.Scheduled, _db.ClassSessions.GetQueryable().Single().Status);
    }

    [Theory]
    [InlineData(ClassSessionStatus.Cancelled)]
    [InlineData(ClassSessionStatus.Completed)]
    public async Task CompleteElapsed_Skips_TerminalStatuses(ClassSessionStatus status)
    {
        SeedClass();
        SeedSession(status, SessionKind.Offline, endTime: _now.AddHours(-1));
        var sut = CreateSut();

        Assert.Equal(0, await sut.CompleteElapsedSessionsAsync());
        Assert.Equal(status, _db.ClassSessions.GetQueryable().Single().Status);
    }

    [Fact]
    public async Task CompleteElapsed_Skips_DeletedSession()
    {
        SeedClass();
        SeedSession(ClassSessionStatus.InProgress, SessionKind.Offline, endTime: _now.AddHours(-1), isDeleted: true);
        var sut = CreateSut();

        Assert.Equal(0, await sut.CompleteElapsedSessionsAsync());
    }

    [Fact]
    public async Task CompleteElapsed_ClosesOpenParticipationSegments()
    {
        SeedClass();
        var endTime = _now.AddMinutes(-30);
        SeedSession(ClassSessionStatus.InProgress, SessionKind.LiveOnline, endTime: endTime);
        _db.SessionAttendances.Seed(new SessionAttendance
        {
            Id = Guid.NewGuid(),
            ClassSessionId = _sessionId,
            StudentId = Guid.NewGuid(),
            ModuleEnrollmentId = Guid.NewGuid(),
            Status = AttendanceStatus.Present,
            CheckedInAt = endTime.AddHours(-1),
            IsDeleted = false,
        });
        var sut = CreateSut();

        await sut.CompleteElapsedSessionsAsync();

        var attendance = _db.SessionAttendances.GetQueryable().Single();
        Assert.Equal(endTime, attendance.LeftAt);
        Assert.Equal(60, attendance.ParticipationMinutes);
    }

    [Fact]
    public async Task CompleteElapsed_WithAcceptedExpert_RequestsFeedback()
    {
        SeedClass();
        SeedSession(ClassSessionStatus.InProgress, SessionKind.Offline, endTime: _now.AddMinutes(-5));
        var expertUserId = Guid.Parse("16161616-1616-1616-1616-161616161616");
        var expertId = Guid.Parse("17171717-1717-1717-1717-171717171717");
        _db.Experts.Seed(new Expert
        {
            Id = expertId,
            Code = "EXP-001",
            FullName = "Dr. Expert",
            UserId = expertUserId,
            IsDeleted = false,
        });
        _db.ClassSessionExperts.Seed(new ClassSessionExpert
        {
            Id = Guid.Parse("18181818-1818-1818-1818-181818181818"),
            ClassSessionId = _sessionId,
            ExpertId = expertId,
            Status = ClassSessionExpertStatus.Accepted,
            IsDeleted = false,
        });
        var sut = CreateSut();

        await sut.CompleteElapsedSessionsAsync();

        _notifications.Verify(
            n => n.PublishManyAsync(
                It.Is<IReadOnlyList<NotificationCommand>>(cmds =>
                    cmds.Any(c => c.Type == NotificationType.ClassSessionCompleted)
                    && cmds.Any(c => c.Type == NotificationType.ClassSessionExpertFeedbackRequested)),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Theory]
    [InlineData(SessionKind.LiveOnline)]
    [InlineData(SessionKind.Offline)]
    public async Task StartDue_MovesScheduledToInProgress_AndPublishesStartedOnce(SessionKind kind)
    {
        SeedClass();
        SeedSession(ClassSessionStatus.Scheduled, kind, endTime: _now.AddHours(2), startTime: _now.AddMinutes(-1));
        var sut = CreateSut();

        var first = await sut.StartDueSessionsAsync();
        var second = await sut.StartDueSessionsAsync();

        Assert.Equal(1, first);
        Assert.Equal(0, second);
        Assert.Equal(ClassSessionStatus.InProgress, _db.ClassSessions.GetQueryable().Single().Status);
        _notifications.Verify(
            n => n.PublishManyAsync(
                It.Is<IReadOnlyList<NotificationCommand>>(cmds =>
                    cmds.Count == 1
                    && cmds[0].Type == NotificationType.ClassSessionStarted
                    && cmds[0].EntityId == _sessionId),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task StartDue_StartsAtExactStartTime()
    {
        SeedClass();
        SeedSession(ClassSessionStatus.Scheduled, SessionKind.Offline, endTime: _now.AddHours(3), startTime: _now);
        var sut = CreateSut();

        Assert.Equal(1, await sut.StartDueSessionsAsync());
    }

    [Fact]
    public async Task StartDue_Skips_BeforeStartTime()
    {
        SeedClass();
        SeedSession(ClassSessionStatus.Scheduled, SessionKind.Offline, endTime: _now.AddHours(3), startTime: _now.AddMinutes(1));
        var sut = CreateSut();

        Assert.Equal(0, await sut.StartDueSessionsAsync());
        Assert.Equal(ClassSessionStatus.Scheduled, _db.ClassSessions.GetQueryable().Single().Status);
        _notifications.Verify(
            n => n.PublishManyAsync(It.IsAny<IReadOnlyList<NotificationCommand>>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task StartDue_Skips_WhenEndTimeAlreadyPassed()
    {
        SeedClass();
        SeedSession(ClassSessionStatus.Scheduled, SessionKind.Offline, endTime: _now);
        var sut = CreateSut();

        Assert.Equal(0, await sut.StartDueSessionsAsync());
        Assert.Equal(1, await sut.CompleteElapsedSessionsAsync());
        Assert.Equal(ClassSessionStatus.Completed, _db.ClassSessions.GetQueryable().Single().Status);
    }

    [Fact]
    public async Task StartDue_Skips_AssignmentWindow()
    {
        SeedClass();
        SeedSession(ClassSessionStatus.Scheduled, SessionKind.AssignmentWindow, endTime: _now.AddDays(2), startTime: _now.AddHours(-1));
        var sut = CreateSut();

        Assert.Equal(0, await sut.StartDueSessionsAsync());
    }

    [Theory]
    [InlineData(ClassSessionStatus.InProgress)]
    [InlineData(ClassSessionStatus.Cancelled)]
    public async Task StartDue_Skips_NonScheduled(ClassSessionStatus status)
    {
        SeedClass();
        SeedSession(status, SessionKind.Offline, endTime: _now.AddHours(2), startTime: _now.AddMinutes(-10));
        var sut = CreateSut();

        Assert.Equal(0, await sut.StartDueSessionsAsync());
        Assert.Equal(status, _db.ClassSessions.GetQueryable().Single().Status);
    }

    [Fact]
    public async Task StartDue_LeavesInvitedCoTeachUntouched()
    {
        SeedClass();
        SeedSession(ClassSessionStatus.Scheduled, SessionKind.Offline, endTime: _now.AddHours(2), startTime: _now.AddMinutes(-1));
        _db.ClassSessionExperts.Seed(new ClassSessionExpert
        {
            Id = Guid.Parse("18181818-1818-1818-1818-181818181818"),
            ClassSessionId = _sessionId,
            ExpertId = Guid.Parse("17171717-1717-1717-1717-171717171717"),
            Status = ClassSessionExpertStatus.Invited,
            IsDeleted = false,
        });
        var sut = CreateSut();

        await sut.StartDueSessionsAsync();

        var invite = _db.ClassSessionExperts.GetQueryable().Single();
        Assert.Equal(ClassSessionExpertStatus.Invited, invite.Status);
        Assert.False(invite.IsDeleted);
    }

    private void SeedClass()
        => _db.Classes.Seed(new Class
        {
            Id = _classId,
            Code = "CLS",
            Name = "Cohort A",
            ProgramId = _programId,
            Status = ClassStatus.InProgress,
            MaxCapacity = 20,
            StartDate = _now.AddDays(-7),
            EndDate = _now.AddDays(30),
            IsDeleted = false,
        });

    private void SeedSession(
        ClassSessionStatus status,
        SessionKind kind,
        DateTime endTime,
        bool isDeleted = false,
        DateTime? startTime = null)
        => _db.ClassSessions.Seed(new ClassSession
        {
            Id = _sessionId,
            ClassId = _classId,
            ModuleId = _moduleId,
            Title = "Lab",
            SessionKind = kind,
            StartTime = startTime ?? endTime.AddHours(-3),
            EndTime = endTime,
            Status = status,
            IsDeleted = isDeleted,
        });
}
