using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OboxSteam.Application.Interfaces;
using OboxSteam.Application.Notifications;
using OboxSteam.Application.Services;
using OboxSteam.Domain.Entities;
using OboxSteam.Domain.Enums;
using OboxSteam.Test.Helpers;

namespace OboxSteam.Test.UnitTests;

public sealed class SessionReminderPublisherTests
{
    private readonly Guid _classId = Guid.Parse("44444444-4444-4444-4444-444444444444");
    private readonly Guid _sessionId = Guid.Parse("55555555-5555-5555-5555-555555555555");
    private readonly Guid _moduleId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private readonly Guid _programId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private readonly DateTime _now = new(2026, 8, 30, 9, 0, 0, DateTimeKind.Utc);

    private readonly InMemoryUnitOfWork _db = new();
    private readonly Mock<ICurrentTime> _currentTime = new();
    private readonly Mock<INotificationPublisher> _notifications = new();

    private SessionReminderPublisher CreateSut()
    {
        _currentTime.Setup(t => t.GetCurrentTime()).Returns(_now);
        _notifications
            .Setup(n => n.PublishManyAsync(It.IsAny<IReadOnlyList<NotificationCommand>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        return new SessionReminderPublisher(
            _db,
            _currentTime.Object,
            _notifications.Object,
            NullLogger<SessionReminderPublisher>.Instance);
    }

    [Fact]
    public async Task PublishDueReminders_SendsOnce_ForSessionWithin30Minutes()
    {
        _db.Classes.Seed(new Class
        {
            Id = _classId,
            Code = "CLS",
            Name = "Cohort A",
            ProgramId = _programId,
            Status = ClassStatus.InProgress,
            MaxCapacity = 20,
            StartDate = _now.AddDays(-1),
            EndDate = _now.AddDays(30),
            IsDeleted = false,
        });
        _db.ClassSessions.Seed(new ClassSession
        {
            Id = _sessionId,
            ClassId = _classId,
            ModuleId = _moduleId,
            Title = "Soon session",
            SessionKind = SessionKind.LiveOnline,
            StartTime = _now.AddMinutes(20),
            EndTime = _now.AddMinutes(80),
            Status = ClassSessionStatus.Scheduled,
            ReminderSentAt = null,
            IsDeleted = false,
        });

        var sut = CreateSut();
        var first = await sut.PublishDueRemindersAsync();
        var second = await sut.PublishDueRemindersAsync();

        Assert.Equal(1, first);
        Assert.Equal(0, second);

        var session = _db.ClassSessions.GetQueryable().Single();
        Assert.Equal(_now, session.ReminderSentAt);

        _notifications.Verify(
            n => n.PublishManyAsync(
                It.Is<IReadOnlyList<NotificationCommand>>(cmds =>
                    cmds.Count == 1
                    && cmds[0].Type == NotificationType.SessionStartingSoon
                    && cmds[0].EntityId == _sessionId),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task PublishDueReminders_Skips_WhenOutsideLeadWindow()
    {
        _db.ClassSessions.Seed(new ClassSession
        {
            Id = _sessionId,
            ClassId = _classId,
            ModuleId = _moduleId,
            Title = "Later",
            SessionKind = SessionKind.Offline,
            StartTime = _now.AddMinutes(45),
            EndTime = _now.AddHours(2),
            Status = ClassSessionStatus.Scheduled,
            IsDeleted = false,
        });

        var sut = CreateSut();
        var count = await sut.PublishDueRemindersAsync();

        Assert.Equal(0, count);
        _notifications.Verify(
            n => n.PublishManyAsync(It.IsAny<IReadOnlyList<NotificationCommand>>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
