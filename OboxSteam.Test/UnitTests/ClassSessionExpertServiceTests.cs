using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OboxSteam.Application.DTOs.ClassSessionExpertDTO;
using OboxSteam.Application.Exceptions;
using OboxSteam.Application.Interfaces;
using OboxSteam.Application.Notifications;
using OboxSteam.Application.Services;
using OboxSteam.Domain.Entities;
using OboxSteam.Domain.Enums;
using OboxSteam.Test.Helpers;

namespace OboxSteam.Test.UnitTests;

public sealed class ClassSessionExpertServiceTests
{
    private readonly Guid _managerId = Guid.Parse("13131313-1313-1313-1313-131313131313");
    private readonly Guid _expertUserId = Guid.Parse("16161616-1616-1616-1616-161616161616");
    private readonly Guid _otherExpertUserId = Guid.Parse("19191919-1919-1919-1919-191919191919");
    private readonly Guid _expertId = Guid.Parse("17171717-1717-1717-1717-171717171717");
    private readonly Guid _otherExpertId = Guid.Parse("20202020-2020-2020-2020-202020202020");
    private readonly Guid _programId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private readonly Guid _moduleId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private readonly Guid _activityId = Guid.Parse("35353535-3535-3535-3535-353535353535");
    private readonly Guid _classId = Guid.Parse("44444444-4444-4444-4444-444444444444");
    private readonly Guid _sessionId = Guid.Parse("55555555-5555-5555-5555-555555555555");
    private readonly Guid _otherSessionId = Guid.Parse("56565656-5656-5656-5656-565656565656");
    private readonly Guid _invitationId = Guid.Parse("18181818-1818-1818-1818-181818181818");
    private readonly DateTime _now = DateTime.UtcNow;

    private readonly InMemoryUnitOfWork _db = new();
    private readonly Mock<IClaimsService> _claimsService = new();
    private readonly Mock<INotificationPublisher> _notificationPublisher = new();

    private ClassSessionExpertService CreateSut(Guid? currentUserId = null)
    {
        _claimsService.Setup(c => c.GetCurrentUserId).Returns(currentUserId ?? _managerId);
        _notificationPublisher
            .Setup(n => n.PublishAsync(It.IsAny<NotificationCommand>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _notificationPublisher
            .Setup(n => n.PublishManyAsync(It.IsAny<IReadOnlyList<NotificationCommand>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        return new ClassSessionExpertService(
            _db,
            _claimsService.Object,
            NullLogger<ClassSessionExpertService>.Instance,
            _notificationPublisher.Object);
    }

    private void SeedUsersAndClass()
    {
        _db.Users.Seed(
            new User
            {
                Id = _managerId,
                Code = "MGR",
                Email = "mgr@test.com",
                FullName = "Manager",
                Role = RoleType.Manager,
                IsDeleted = false,
            },
            new User
            {
                Id = _expertUserId,
                Code = "EXP-USR",
                Email = "exp@test.com",
                FullName = "Dr. Expert",
                Role = RoleType.Expert,
                IsDeleted = false,
            },
            new User
            {
                Id = _otherExpertUserId,
                Code = "EXP-USR-2",
                Email = "exp2@test.com",
                FullName = "Other Expert",
                Role = RoleType.Expert,
                IsDeleted = false,
            });

        _db.Classes.Seed(new Class
        {
            Id = _classId,
            Code = "CLS-001",
            Name = "Cohort A",
            ProgramId = _programId,
            Status = ClassStatus.InProgress,
            MaxCapacity = 30,
            StartDate = _now.AddDays(-7),
            EndDate = _now.AddDays(60),
            IsDeleted = false,
        });

        _db.ClassSessions.Seed(new ClassSession
        {
            Id = _sessionId,
            ClassId = _classId,
            ModuleId = _moduleId,
            ActivityId = _activityId,
            Title = "Lab Offline",
            SessionKind = SessionKind.Offline,
            StartTime = _now.AddDays(1),
            EndTime = _now.AddDays(1).AddHours(2),
            Status = ClassSessionStatus.Scheduled,
            IsDeleted = false,
        });

        _db.Experts.Seed(
            new Expert
            {
                Id = _expertId,
                Code = "EXP-001",
                FullName = "Dr. Expert",
                UserId = _expertUserId,
                IsDeleted = false,
            },
            new Expert
            {
                Id = _otherExpertId,
                Code = "EXP-002",
                FullName = "Other Expert",
                UserId = _otherExpertUserId,
                IsDeleted = false,
            });

        _db.ProgramBoards.Seed(
            new ProgramBoard
            {
                Id = Guid.Parse("77777777-7777-7777-7777-777777777777"),
                ProgramId = _programId,
                ExpertId = _expertId,
                IsDeleted = false,
            },
            new ProgramBoard
            {
                Id = Guid.Parse("78787878-7878-7878-7878-787878787878"),
                ProgramId = _programId,
                ExpertId = _otherExpertId,
                IsDeleted = false,
            });
    }

    private ClassSessionExpert SeedInvitation(ClassSessionExpertStatus status = ClassSessionExpertStatus.Invited)
    {
        var invitation = new ClassSessionExpert
        {
            Id = _invitationId,
            ClassSessionId = _sessionId,
            ExpertId = _expertId,
            Status = status,
            IsDeleted = false,
            CreatedAt = _now,
        };
        _db.ClassSessionExperts.Seed(invitation);
        return invitation;
    }

    [Fact]
    public async Task Invite_CreatesInvitedRow_AndNotifiesExpert()
    {
        SeedUsersAndClass();
        var sut = CreateSut();

        var result = await sut.InviteAsync(new InviteClassSessionExpertDto
        {
            ClassSessionId = _sessionId,
            ExpertId = _expertId,
        });

        Assert.Equal(ClassSessionExpertStatus.Invited, result.Status);
        Assert.Equal(_expertId, result.ExpertId);
        Assert.Single(_db.ClassSessionExperts.Items);
        _notificationPublisher.Verify(
            n => n.PublishAsync(
                It.Is<NotificationCommand>(c => c.Type == NotificationType.ClassSessionExpertInvited),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Invite_Throws_WhenSessionAlreadyHasActiveExpert()
    {
        SeedUsersAndClass();
        SeedInvitation();
        var sut = CreateSut();

        await Assert.ThrowsAsync<ConflictException>(() =>
            sut.InviteAsync(new InviteClassSessionExpertDto
            {
                ClassSessionId = _sessionId,
                ExpertId = _otherExpertId,
            }));
    }

    [Fact]
    public async Task Invite_Throws_WhenNotOffline()
    {
        SeedUsersAndClass();
        _db.ClassSessions.Items[0].SessionKind = SessionKind.LiveOnline;
        var sut = CreateSut();

        await Assert.ThrowsAsync<BadRequestException>(() =>
            sut.InviteAsync(new InviteClassSessionExpertDto
            {
                ClassSessionId = _sessionId,
                ExpertId = _expertId,
            }));
    }

    [Fact]
    public async Task Invite_Allowed_AfterDecline()
    {
        SeedUsersAndClass();
        SeedInvitation(ClassSessionExpertStatus.Declined);
        var sut = CreateSut();

        var result = await sut.InviteAsync(new InviteClassSessionExpertDto
        {
            ClassSessionId = _sessionId,
            ExpertId = _otherExpertId,
        });

        Assert.Equal(ClassSessionExpertStatus.Invited, result.Status);
        Assert.Equal(_otherExpertId, result.ExpertId);
        Assert.Equal(2, _db.ClassSessionExperts.Items.Count);
    }

    [Fact]
    public async Task Accept_SetsAccepted()
    {
        SeedUsersAndClass();
        SeedInvitation();
        var sut = CreateSut(_expertUserId);

        var result = await sut.AcceptAsync(_invitationId);

        Assert.Equal(ClassSessionExpertStatus.Accepted, result.Status);
        _notificationPublisher.Verify(
            n => n.PublishAsync(
                It.Is<NotificationCommand>(c => c.Type == NotificationType.ClassSessionExpertAccepted),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Accept_Throws_WhenOverlappingAcceptedSession()
    {
        SeedUsersAndClass();
        SeedInvitation();
        var session = _db.ClassSessions.Items[0];
        _db.ClassSessions.Seed(new ClassSession
        {
            Id = _otherSessionId,
            ClassId = _classId,
            ModuleId = _moduleId,
            Title = "Other lab",
            SessionKind = SessionKind.Offline,
            StartTime = session.StartTime.AddMinutes(30),
            EndTime = session.EndTime.AddMinutes(30),
            Status = ClassSessionStatus.Scheduled,
            IsDeleted = false,
        });
        _db.ClassSessionExperts.Seed(new ClassSessionExpert
        {
            Id = Guid.NewGuid(),
            ClassSessionId = _otherSessionId,
            ExpertId = _expertId,
            Status = ClassSessionExpertStatus.Accepted,
            IsDeleted = false,
        });
        var sut = CreateSut(_expertUserId);

        await Assert.ThrowsAsync<ConflictException>(() => sut.AcceptAsync(_invitationId));
        Assert.Equal(ClassSessionExpertStatus.Invited, _db.ClassSessionExperts.Items.Single(e => e.Id == _invitationId).Status);
    }

    [Fact]
    public async Task Withdraw_SoftDeletesInvited()
    {
        SeedUsersAndClass();
        SeedInvitation();
        var sut = CreateSut();

        await sut.WithdrawAsync(_invitationId);

        Assert.True(_db.ClassSessionExperts.Items.Single(e => e.Id == _invitationId).IsDeleted);
        _notificationPublisher.Verify(
            n => n.PublishAsync(
                It.Is<NotificationCommand>(c => c.Type == NotificationType.ClassSessionExpertInvitationWithdrawn),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Withdraw_Throws_WhenAccepted()
    {
        SeedUsersAndClass();
        SeedInvitation(ClassSessionExpertStatus.Accepted);
        var sut = CreateSut();

        await Assert.ThrowsAsync<ConflictException>(() => sut.WithdrawAsync(_invitationId));
        Assert.False(_db.ClassSessionExperts.Items.Single(e => e.Id == _invitationId).IsDeleted);
    }

    [Fact]
    public async Task ApproveReschedule_AppliesProposedTimes()
    {
        SeedUsersAndClass();
        SeedInvitation(ClassSessionExpertStatus.Accepted);
        var session = _db.ClassSessions.Items[0];
        var originalStart = session.StartTime;
        session.ProposedStartTime = originalStart.AddDays(3);
        session.ProposedEndTime = originalStart.AddDays(3).AddHours(2);
        var sut = CreateSut(_expertUserId);

        var result = await sut.ApproveRescheduleAsync(_invitationId);

        Assert.Null(_db.ClassSessions.Items[0].ProposedStartTime);
        Assert.Equal(originalStart.AddDays(3), _db.ClassSessions.Items[0].StartTime);
        Assert.Equal(originalStart.AddDays(3), result.SessionStartTime);
        Assert.Equal(ClassSessionExpertStatus.Accepted, result.Status);
        _notificationPublisher.Verify(
            n => n.PublishAsync(
                It.Is<NotificationCommand>(c => c.Type == NotificationType.ClassSessionRescheduled),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task DeclineReschedule_ClearsProposal_KeepsAcceptedAndOldTime()
    {
        SeedUsersAndClass();
        SeedInvitation(ClassSessionExpertStatus.Accepted);
        var session = _db.ClassSessions.Items[0];
        var originalStart = session.StartTime;
        session.ProposedStartTime = originalStart.AddDays(3);
        session.ProposedEndTime = originalStart.AddDays(3).AddHours(2);
        var sut = CreateSut(_expertUserId);

        var result = await sut.DeclineRescheduleAsync(_invitationId);

        Assert.Equal(originalStart, _db.ClassSessions.Items[0].StartTime);
        Assert.Null(_db.ClassSessions.Items[0].ProposedStartTime);
        Assert.Equal(ClassSessionExpertStatus.Accepted, result.Status);
        _notificationPublisher.Verify(
            n => n.PublishAsync(
                It.Is<NotificationCommand>(c => c.Type == NotificationType.ClassSessionExpertRescheduleDeclined),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task SubmitFeedback_SavesCommentAndRating_AndNotifiesMentor()
    {
        SeedUsersAndClass();
        SeedInvitation(ClassSessionExpertStatus.Accepted);
        _db.ClassSessions.Items[0].Status = ClassSessionStatus.Completed;
        var sut = CreateSut(_expertUserId);

        var result = await sut.SubmitFeedbackAsync(_invitationId, new SubmitClassSessionExpertFeedbackDto
        {
            Comment = "  Mentor dẫn dắt rõ, nên chừa thời gian Q&A.  ",
            Rating = 4,
        });

        var stored = _db.ClassSessionExperts.Items.Single(e => e.Id == _invitationId);
        Assert.Equal("Mentor dẫn dắt rõ, nên chừa thời gian Q&A.", stored.MentorFeedback);
        Assert.Equal(4, stored.MentorFeedbackRating);
        Assert.NotNull(stored.MentorFeedbackAt);
        Assert.Equal(stored.MentorFeedback, result.MentorFeedback);
        Assert.Equal(4, result.MentorFeedbackRating);
        _notificationPublisher.Verify(
            n => n.PublishAsync(
                It.Is<NotificationCommand>(c =>
                    c.Type == NotificationType.ClassSessionExpertFeedbackSubmitted
                    && c.Audience.Kind == NotificationAudienceKind.ClassMentor),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task SubmitFeedback_UpsertsExistingFeedback()
    {
        SeedUsersAndClass();
        var invitation = SeedInvitation(ClassSessionExpertStatus.Accepted);
        invitation.MentorFeedback = "First note";
        invitation.MentorFeedbackRating = 3;
        invitation.MentorFeedbackAt = _now.AddHours(-2);
        _db.ClassSessions.Items[0].Status = ClassSessionStatus.Completed;
        var sut = CreateSut(_expertUserId);

        var result = await sut.SubmitFeedbackAsync(_invitationId, new SubmitClassSessionExpertFeedbackDto
        {
            Comment = "Updated overview",
            Rating = 5,
        });

        Assert.Equal("Updated overview", result.MentorFeedback);
        Assert.Equal(5, result.MentorFeedbackRating);
        Assert.True(result.MentorFeedbackAt > _now.AddHours(-1));
        _notificationPublisher.Verify(
            n => n.PublishAsync(
                It.Is<NotificationCommand>(c => c.Type == NotificationType.ClassSessionExpertFeedbackSubmitted),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task SubmitFeedback_Throws_WhenInvited()
    {
        SeedUsersAndClass();
        SeedInvitation();
        _db.ClassSessions.Items[0].Status = ClassSessionStatus.Completed;
        var sut = CreateSut(_expertUserId);

        await Assert.ThrowsAsync<BadRequestException>(() =>
            sut.SubmitFeedbackAsync(_invitationId, new SubmitClassSessionExpertFeedbackDto
            {
                Comment = "Too soon",
                Rating = 4,
            }));
    }

    [Fact]
    public async Task SubmitFeedback_Throws_WhenDeclined()
    {
        SeedUsersAndClass();
        SeedInvitation(ClassSessionExpertStatus.Declined);
        _db.ClassSessions.Items[0].Status = ClassSessionStatus.Completed;
        var sut = CreateSut(_expertUserId);

        await Assert.ThrowsAsync<BadRequestException>(() =>
            sut.SubmitFeedbackAsync(_invitationId, new SubmitClassSessionExpertFeedbackDto
            {
                Comment = "Declined expert",
                Rating = 4,
            }));
    }

    [Fact]
    public async Task SubmitFeedback_Throws_WhenSessionNotCompleted()
    {
        SeedUsersAndClass();
        SeedInvitation(ClassSessionExpertStatus.Accepted);
        var sut = CreateSut(_expertUserId);

        await Assert.ThrowsAsync<ConflictException>(() =>
            sut.SubmitFeedbackAsync(_invitationId, new SubmitClassSessionExpertFeedbackDto
            {
                Comment = "Still scheduled",
                Rating = 4,
            }));
    }

    [Fact]
    public async Task SubmitFeedback_Throws_WhenSessionCancelled()
    {
        SeedUsersAndClass();
        SeedInvitation(ClassSessionExpertStatus.Accepted);
        _db.ClassSessions.Items[0].Status = ClassSessionStatus.Cancelled;
        var sut = CreateSut(_expertUserId);

        await Assert.ThrowsAsync<ConflictException>(() =>
            sut.SubmitFeedbackAsync(_invitationId, new SubmitClassSessionExpertFeedbackDto
            {
                Comment = "Cancelled",
                Rating = 4,
            }));
    }

    [Fact]
    public async Task SubmitFeedback_Throws_WhenCommentBlank()
    {
        SeedUsersAndClass();
        SeedInvitation(ClassSessionExpertStatus.Accepted);
        _db.ClassSessions.Items[0].Status = ClassSessionStatus.Completed;
        var sut = CreateSut(_expertUserId);

        await Assert.ThrowsAsync<BadRequestException>(() =>
            sut.SubmitFeedbackAsync(_invitationId, new SubmitClassSessionExpertFeedbackDto
            {
                Comment = "   ",
                Rating = 4,
            }));
    }

    [Fact]
    public async Task SubmitFeedback_Throws_WhenRatingOutOfRange()
    {
        SeedUsersAndClass();
        SeedInvitation(ClassSessionExpertStatus.Accepted);
        _db.ClassSessions.Items[0].Status = ClassSessionStatus.Completed;
        var sut = CreateSut(_expertUserId);

        await Assert.ThrowsAsync<BadRequestException>(() =>
            sut.SubmitFeedbackAsync(_invitationId, new SubmitClassSessionExpertFeedbackDto
            {
                Comment = "Valid comment",
                Rating = 0,
            }));
    }

    [Fact]
    public async Task SubmitFeedback_Throws_WhenOtherExpert()
    {
        SeedUsersAndClass();
        SeedInvitation(ClassSessionExpertStatus.Accepted);
        _db.ClassSessions.Items[0].Status = ClassSessionStatus.Completed;
        var sut = CreateSut(_otherExpertUserId);

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            sut.SubmitFeedbackAsync(_invitationId, new SubmitClassSessionExpertFeedbackDto
            {
                Comment = "Not my invitation",
                Rating = 4,
            }));
    }
}
