using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OboxSteam.Application.Exceptions;
using OboxSteam.Application.Interfaces;
using OboxSteam.Application.Services;
using OboxSteam.Domain.Entities;
using OboxSteam.Domain.Enums;
using OboxSteam.Test.Helpers;

namespace OboxSteam.Test.UnitTests;

public sealed class NotificationServiceTests
{
    private readonly Guid _userId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private readonly Guid _otherUserId = Guid.Parse("12121212-1212-1212-1212-121212121212");
    private readonly Guid _notificationId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private readonly Guid _readNotificationId = Guid.Parse("23232323-2323-2323-2323-232323232323");
    private readonly Guid _otherNotificationId = Guid.Parse("24242424-2424-2424-2424-242424242424");

    private readonly DateTime _now = DateTime.UtcNow;
    private readonly InMemoryUnitOfWork _db = new();
    private readonly Mock<IClaimsService> _claimsService = new();

    private NotificationService CreateSut(Guid? userId = null)
    {
        _claimsService.Setup(c => c.GetCurrentUserId).Returns(userId ?? _userId);
        return new NotificationService(
            _db,
            _claimsService.Object,
            NullLogger<NotificationService>.Instance);
    }

    private void SeedNotification(
        Guid id,
        Guid recipientId,
        string title,
        DateTime createdAt,
        DateTime? readAt = null)
    {
        _db.Notifications.Seed(new Notification
        {
            Id = id,
            RecipientUserId = recipientId,
            Type = NotificationType.AccountRegistered,
            Title = title,
            Body = "Body",
            CreatedAt = createdAt,
            ReadAt = readAt,
            IsDeleted = false,
        });
    }

    [Fact]
    public async Task GetMyNotifications_ReturnsPagedOrdered()
    {
        SeedNotification(_notificationId, _userId, "Newer", _now.AddHours(-1));
        SeedNotification(_readNotificationId, _userId, "Older", _now.AddHours(-3));
        SeedNotification(_otherNotificationId, _otherUserId, "Other", _now);
        var sut = CreateSut();

        var result = await sut.GetMyNotificationsAsync(1, 10, unreadOnly: null);

        Assert.Equal(2, result.TotalCount);
        Assert.Equal("Newer", result.Items[0].Title);
        Assert.Equal("Older", result.Items[1].Title);
    }

    [Fact]
    public async Task GetMyNotifications_FiltersUnreadOnly()
    {
        SeedNotification(_notificationId, _userId, "Unread", _now, readAt: null);
        SeedNotification(_readNotificationId, _userId, "Read", _now, readAt: _now.AddMinutes(-5));
        var sut = CreateSut();

        var result = await sut.GetMyNotificationsAsync(1, 10, unreadOnly: true);

        Assert.Equal(1, result.TotalCount);
        Assert.Equal(_notificationId, result.Items[0].Id);
    }

    [Fact]
    public async Task GetMyNotifications_ClampsPagination()
    {
        SeedNotification(_notificationId, _userId, "One", _now);
        var sut = CreateSut();

        var lowPage = await sut.GetMyNotificationsAsync(0, 0, null);
        var highPageSize = await sut.GetMyNotificationsAsync(1, 100, null);

        Assert.Equal(1, lowPage.CurrentPage);
        Assert.Equal(10, lowPage.PageSize);
        Assert.Equal(50, highPageSize.PageSize);
    }

    [Fact]
    public async Task GetMyNotifications_Throws_WhenUnauthorized()
    {
        var sut = CreateSut(Guid.Empty);

        await Assert.ThrowsAsync<UnauthorizedException>(() =>
            sut.GetMyNotificationsAsync(1, 10, null));
    }

    [Fact]
    public async Task GetUnreadCount_ReturnsCount()
    {
        SeedNotification(_notificationId, _userId, "Unread", _now);
        SeedNotification(_readNotificationId, _userId, "Read", _now, readAt: _now);
        SeedNotification(_otherNotificationId, _otherUserId, "Other", _now);
        var sut = CreateSut();

        var result = await sut.GetUnreadCountAsync();

        Assert.Equal(1, result.Count);
    }

    [Fact]
    public async Task MarkRead_SetsReadAt()
    {
        SeedNotification(_notificationId, _userId, "Unread", _now);
        var sut = CreateSut();

        await sut.MarkReadAsync(_notificationId);

        Assert.NotNull(_db.Notifications.Items.Single(n => n.Id == _notificationId).ReadAt);
    }

    [Fact]
    public async Task MarkRead_IsIdempotent()
    {
        var readAt = _now.AddHours(-1);
        SeedNotification(_notificationId, _userId, "Read", _now, readAt: readAt);
        var sut = CreateSut();

        await sut.MarkReadAsync(_notificationId);

        Assert.Equal(readAt, _db.Notifications.Items.Single().ReadAt);
    }

    [Fact]
    public async Task MarkRead_Throws_WhenNotFoundOrOtherUser()
    {
        SeedNotification(_notificationId, _otherUserId, "Other", _now);
        var sut = CreateSut();

        await Assert.ThrowsAsync<NotFoundException>(() => sut.MarkReadAsync(_notificationId));
        await Assert.ThrowsAsync<NotFoundException>(() => sut.MarkReadAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task MarkAllRead_MarksAllUnread()
    {
        SeedNotification(_notificationId, _userId, "A", _now);
        SeedNotification(_readNotificationId, _userId, "B", _now, readAt: _now.AddMinutes(-5));
        SeedNotification(_otherNotificationId, _otherUserId, "C", _now);
        var sut = CreateSut();

        await sut.MarkAllReadAsync();

        Assert.NotNull(_db.Notifications.Items.Single(n => n.Id == _notificationId).ReadAt);
        Assert.NotNull(_db.Notifications.Items.Single(n => n.Id == _readNotificationId).ReadAt);
        Assert.Null(_db.Notifications.Items.Single(n => n.Id == _otherNotificationId).ReadAt);
    }

    [Fact]
    public async Task MarkAllRead_NoOp_WhenNoneUnread()
    {
        SeedNotification(_notificationId, _userId, "Read", _now, readAt: _now);
        var sut = CreateSut();

        await sut.MarkAllReadAsync();

        Assert.Equal(0, _db.SaveChangesCallCount);
    }
}
