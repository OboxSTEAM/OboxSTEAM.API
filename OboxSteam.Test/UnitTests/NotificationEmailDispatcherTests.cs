using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OboxSteam.Application.DTOs.EmailDTO;
using OboxSteam.Application.DTOs.NotificationDTO;
using OboxSteam.Application.Interfaces;
using OboxSteam.Application.Services;
using OboxSteam.Domain.Entities;
using OboxSteam.Domain.Enums;
using OboxSteam.Test.Helpers;

namespace OboxSteam.Test.UnitTests;

public sealed class NotificationEmailDispatcherTests
{
    private readonly Guid _activeUserId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private readonly Guid _lockedUserId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

    [Fact]
    public async Task DispatchMany_SendsInboxEmail_ForPriorityTypeAndActiveUser()
    {
        var db = new InMemoryUnitOfWork();
        SeedUser(db, _activeUserId, AccountStatus.Active, "active@test.com");

        var email = new Mock<IEmailService>();
        var sut = new NotificationEmailDispatcher(
            db,
            email.Object,
            NullLogger<NotificationEmailDispatcher>.Instance);

        await sut.DispatchManyAsync(
        [
            new NotificationDto
            {
                Id = Guid.NewGuid(),
                RecipientUserId = _activeUserId,
                Type = NotificationType.PaymentFailed,
                Title = "Thanh toán thất bại",
                Body = "Thanh toán của bạn không thể hoàn tất. Vui lòng thử lại."
            }
        ]);

        email.Verify(
            e => e.SendInboxNotificationEmailAsync(It.Is<InboxNotificationEmailDto>(dto =>
                dto.To == "active@test.com"
                && dto.Title == "Thanh toán thất bại"
                && dto.Body == "Thanh toán của bạn không thể hoàn tất. Vui lòng thử lại.")),
            Times.Once);
    }

    [Fact]
    public async Task DispatchMany_SkipsNonPriorityType()
    {
        var db = new InMemoryUnitOfWork();
        SeedUser(db, _activeUserId, AccountStatus.Active, "active@test.com");

        var email = new Mock<IEmailService>();
        var sut = new NotificationEmailDispatcher(
            db,
            email.Object,
            NullLogger<NotificationEmailDispatcher>.Instance);

        await sut.DispatchManyAsync(
        [
            new NotificationDto
            {
                Id = Guid.NewGuid(),
                RecipientUserId = _activeUserId,
                Type = NotificationType.ModuleCompleted,
                Title = "Hoàn thành học phần",
                Body = "Bạn đã hoàn thành \"Robotics 1\"."
            }
        ]);

        email.Verify(
            e => e.SendInboxNotificationEmailAsync(It.IsAny<InboxNotificationEmailDto>()),
            Times.Never);
    }

    [Fact]
    public async Task DispatchMany_SkipsLockedUserAndEmptyEmail()
    {
        var db = new InMemoryUnitOfWork();
        SeedUser(db, _lockedUserId, AccountStatus.Locked, "locked@test.com");
        SeedUser(db, _activeUserId, AccountStatus.Active, " ");

        var email = new Mock<IEmailService>();
        var sut = new NotificationEmailDispatcher(
            db,
            email.Object,
            NullLogger<NotificationEmailDispatcher>.Instance);

        await sut.DispatchManyAsync(
        [
            new NotificationDto
            {
                Id = Guid.NewGuid(),
                RecipientUserId = _lockedUserId,
                Type = NotificationType.PaymentFailed,
                Title = "Thanh toán thất bại"
            },
            new NotificationDto
            {
                Id = Guid.NewGuid(),
                RecipientUserId = _activeUserId,
                Type = NotificationType.PaymentFailed,
                Title = "Thanh toán thất bại"
            }
        ]);

        email.Verify(
            e => e.SendInboxNotificationEmailAsync(It.IsAny<InboxNotificationEmailDto>()),
            Times.Never);
    }

    [Fact]
    public async Task DispatchMany_ContinuesWhenOneEmailFails()
    {
        var secondUserId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");
        var db = new InMemoryUnitOfWork();
        SeedUser(db, _activeUserId, AccountStatus.Active, "first@test.com");
        SeedUser(db, secondUserId, AccountStatus.Active, "second@test.com");

        var email = new Mock<IEmailService>();
        email
            .Setup(e => e.SendInboxNotificationEmailAsync(It.Is<InboxNotificationEmailDto>(d => d.To == "first@test.com")))
            .ThrowsAsync(new InvalidOperationException("Resend down"));
        email
            .Setup(e => e.SendInboxNotificationEmailAsync(It.Is<InboxNotificationEmailDto>(d => d.To == "second@test.com")))
            .Returns(Task.CompletedTask);

        var sut = new NotificationEmailDispatcher(
            db,
            email.Object,
            NullLogger<NotificationEmailDispatcher>.Instance);

        await sut.DispatchManyAsync(
        [
            new NotificationDto
            {
                Id = Guid.NewGuid(),
                RecipientUserId = _activeUserId,
                Type = NotificationType.ResearchReturnedForRevision,
                Title = "Bài nộp được trả lại để chỉnh sửa"
            },
            new NotificationDto
            {
                Id = Guid.NewGuid(),
                RecipientUserId = secondUserId,
                Type = NotificationType.ResearchReturnedForRevision,
                Title = "Bài nộp được trả lại để chỉnh sửa"
            }
        ]);

        email.Verify(
            e => e.SendInboxNotificationEmailAsync(It.Is<InboxNotificationEmailDto>(d => d.To == "second@test.com")),
            Times.Once);
    }

    private static void SeedUser(InMemoryUnitOfWork db, Guid id, AccountStatus status, string email)
    {
        db.Users.Seed(new User
        {
            Id = id,
            Code = "USR-" + id.ToString("N")[..6],
            Email = email,
            FullName = "Test",
            Role = RoleType.Student,
            Status = status,
            IsDeleted = false
        });
    }
}
