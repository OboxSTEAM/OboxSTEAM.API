using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OboxSteam.Application.DTOs.ProgramAdvisoryDTO;
using OboxSteam.Application.DTOs.ProgramDTO;
using OboxSteam.Application.Exceptions;
using OboxSteam.Application.Interfaces;
using OboxSteam.Application.Notifications;
using OboxSteam.Application.Realtime;
using OboxSteam.Application.Services;
using OboxSteam.Domain.Entities;
using OboxSteam.Domain.Enums;
using OboxSteam.Test.Helpers;

namespace OboxSteam.Test.UnitTests;

public sealed class ProgramApprovalServiceTests
{
    private readonly Guid _managerId = Guid.Parse("13131313-1313-1313-1313-131313131313");
    private readonly Guid _advisorUserId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private readonly Guid _boardUserId = Guid.Parse("12121212-1212-1212-1212-121212121212");
    private readonly Guid _advisorExpertId = Guid.Parse("66666666-6666-6666-6666-666666666666");
    private readonly Guid _boardExpertId = Guid.Parse("67676767-6767-6767-6767-676767676767");
    private readonly Guid _programId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private readonly Guid _moduleId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private readonly Guid _frameworkId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private readonly Guid _frameworkVersionId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
    private readonly DateTime _now = new(2026, 10, 1, 12, 0, 0, DateTimeKind.Utc);

    private readonly InMemoryUnitOfWork _db = new();
    private readonly Mock<IClaimsService> _claimsService = new();
    private readonly Mock<ICurrentTime> _currentTime = new();
    private readonly Mock<INotificationPublisher> _notificationPublisher = new();
    private readonly List<NotificationCommand> _published = [];
    private readonly FakeSyncEventPublisher _sync = new();

    public ProgramApprovalServiceTests()
    {
        _currentTime.Setup(c => c.GetCurrentTime()).Returns(() => _now);
        _notificationPublisher
            .Setup(n => n.PublishAsync(It.IsAny<NotificationCommand>(), It.IsAny<CancellationToken>()))
            .Callback<NotificationCommand, CancellationToken>((command, _) => _published.Add(command))
            .Returns(Task.CompletedTask);
        SeedBase();
    }

    private Program TheProgram => _db.Programs.Items.Single();

    private ProgramApprovalService Sut(Guid userId)
    {
        _claimsService.Setup(c => c.GetCurrentUserId).Returns(userId);
        var programService = new ProgramService(
            _db,
            Mock.Of<IBlobService>(),
            NullLogger<ProgramService>.Instance);
        return new ProgramApprovalService(
            _db,
            _claimsService.Object,
            _currentTime.Object,
            _notificationPublisher.Object,
            _sync,
            programService);
    }

    private void SeedUser(Guid id, RoleType role, string code)
        => _db.Users.Seed(new User
        {
            Id = id,
            Code = code,
            Email = $"{code.ToLower()}@test.com",
            FullName = code,
            Role = role,
            Status = AccountStatus.Active,
        });

    private void SeedExpert(Guid expertId, Guid? userId, string code)
        => _db.Experts.Seed(new Expert { Id = expertId, Code = code, FullName = code, UserId = userId });

    private void SeedBase()
    {
        SeedUser(_managerId, RoleType.Manager, "USR-MGR");
        SeedUser(_advisorUserId, RoleType.Expert, "USR-ADV");
        SeedUser(_boardUserId, RoleType.Expert, "USR-BRD");
        SeedExpert(_advisorExpertId, _advisorUserId, "EXP-ADV");
        SeedExpert(_boardExpertId, _boardUserId, "EXP-BRD");
        _db.Programs.Seed(new Program
        {
            Id = _programId,
            Code = "PRG-001",
            Name = "Robotics",
            Category = ProgramCategory.Technology,
            Level = DifficultyLevel.Beginner,
            Status = ProgramStatus.Draft,
            AdvisorExpertId = _advisorExpertId,
            CurriculumVersion = 3,
        });
        _db.ProgramBoards.Seed(new ProgramBoard
        {
            Id = Guid.NewGuid(),
            ProgramId = _programId,
            ExpertId = _boardExpertId,
            RoleInBoard = "Reviewer",
        });
        var module = new Module
        {
            Id = _moduleId,
            ProgramId = _programId,
            Code = "MOD-001",
            Name = "Intro",
            ModuleType = ModuleType.Theory,
            ModuleOrder = 1,
        };
        _db.Modules.Seed(module);
        TheProgram.Modules = [module];
    }

    private void PinFramework(int minModules)
    {
        _db.ProgramFrameworks.Seed(new ProgramFramework
        {
            Id = _frameworkId,
            ExpertId = _advisorExpertId,
            Name = "Blueprint",
            Category = ProgramCategory.Technology,
        });
        _db.ProgramFrameworkVersions.Seed(new ProgramFrameworkVersion
        {
            Id = _frameworkVersionId,
            FrameworkId = _frameworkId,
            VersionNumber = 1,
            IsPublished = true,
            PublishedAt = _now,
            MinModules = minModules,
        });
        TheProgram.FrameworkId = _frameworkId;
        TheProgram.FrameworkVersionId = _frameworkVersionId;
    }

    private ProgramAdvisoryDiscussionMessage SeedPin(DiscussionPinStatus status, long sequence)
    {
        var message = new ProgramAdvisoryDiscussionMessage
        {
            Id = Guid.NewGuid(),
            ProgramId = _programId,
            AuthorUserId = _boardUserId,
            Sequence = sequence,
            Text = $"Pin {sequence}",
            ClientMessageId = $"pin-{sequence}",
            PinStatus = status,
            PinnedByUserId = _boardUserId,
            PinnedAt = _now,
        };
        _db.ProgramAdvisoryDiscussionMessages.Seed(message);
        return message;
    }

    private ProgramApproval SeedApproval(long version, DateTime? revokedAt = null)
    {
        var approval = new ProgramApproval
        {
            Id = Guid.NewGuid(),
            ProgramId = _programId,
            CurriculumVersion = version,
            ApprovedByExpertId = _advisorExpertId,
            ApprovedAt = _now.AddDays(-1),
            RevokedAt = revokedAt,
        };
        _db.ProgramApprovals.Seed(approval);
        return approval;
    }

    private void SeedChange(long version, Guid targetId)
        => _db.CurriculumChanges.Seed(new CurriculumChange
        {
            Id = Guid.NewGuid(),
            ProgramId = _programId,
            Version = version,
            At = _now,
            TargetType = ProgramAdvisoryTargetType.Module,
            TargetId = targetId,
            ChangeKind = CurriculumChangeKind.Created,
            LabelSnapshot = "Module",
        });

    private ProgramAdvisoryDiscussionMessage LastSystemMessage()
        => _db.ProgramAdvisoryDiscussionMessages.Items
            .Where(m => m.Kind == DiscussionMessageKind.System)
            .OrderBy(m => m.Sequence)
            .Last();

    [Fact]
    public async Task RequestApproval_ManagerDraft_PostsSystemMessageAndNotifiesAdvisor()
    {
        var workspace = await Sut(_managerId).RequestApprovalAsync(_programId);

        Assert.Equal(ProgramStatus.Draft, workspace.Status);
        Assert.Equal(DiscussionSystemEventCode.ApprovalRequested, LastSystemMessage().SystemEventCode);
        var notification = Assert.Single(_published);
        Assert.Equal(NotificationType.CurriculumApprovalRequested, notification.Type);
        Assert.Equal(_advisorUserId, notification.Audience.UserId);
    }

    [Fact]
    public async Task RequestApproval_RejectsNonManagerWrongStatusAndMissingAdvisorLogin()
    {
        await Assert.ThrowsAsync<ForbiddenException>(() => Sut(_advisorUserId).RequestApprovalAsync(_programId));

        TheProgram.Status = ProgramStatus.Approved;
        var status = await Assert.ThrowsAsync<ConflictException>(() => Sut(_managerId).RequestApprovalAsync(_programId));
        Assert.Equal("INVALID_STATUS", status.ErrorCode);

        TheProgram.Status = ProgramStatus.Draft;
        _db.Users.Items.Single(u => u.Id == _advisorUserId).Status = AccountStatus.Locked;
        var login = await Assert.ThrowsAsync<BadRequestException>(() => Sut(_managerId).RequestApprovalAsync(_programId));
        Assert.Equal("ADVISOR_LOGIN_REQUIRED", login.ErrorCode);

        TheProgram.AdvisorExpertId = null;
        var advisor = await Assert.ThrowsAsync<BadRequestException>(() => Sut(_managerId).RequestApprovalAsync(_programId));
        Assert.Equal("ADVISOR_REQUIRED", advisor.ErrorCode);
        Assert.Empty(_published);
    }

    [Fact]
    public async Task Approve_OnlyAdvisor_OnDraft_AtCurrentVersion()
    {
        var request = new ApproveProgramRequest { CurriculumVersion = 3 };

        await Assert.ThrowsAsync<ForbiddenException>(() => Sut(_boardUserId).ApproveAsync(_programId, request));
        await Assert.ThrowsAsync<ForbiddenException>(() => Sut(_managerId).ApproveAsync(_programId, request));

        var stale = await Assert.ThrowsAsync<ConflictException>(() => Sut(_advisorUserId).ApproveAsync(
            _programId,
            new ApproveProgramRequest { CurriculumVersion = 2 }));
        Assert.Equal("CURRICULUM_VERSION_STALE", stale.ErrorCode);

        TheProgram.Status = ProgramStatus.Active;
        var status = await Assert.ThrowsAsync<ConflictException>(() => Sut(_advisorUserId).ApproveAsync(_programId, request));
        Assert.Equal("INVALID_STATUS", status.ErrorCode);
        Assert.Empty(_db.ProgramApprovals.Items);
    }

    [Fact]
    public async Task Approve_BlockedByOpenPin_IgnoresRemovedPins()
    {
        SeedPin(DiscussionPinStatus.Open, 1);
        var removed = SeedPin(DiscussionPinStatus.Open, 2);
        removed.RemovedAt = _now;

        var blocked = await Assert.ThrowsAsync<ConflictException>(() => Sut(_advisorUserId).ApproveAsync(
            _programId,
            new ApproveProgramRequest { CurriculumVersion = 3 }));

        Assert.Equal("APPROVAL_BLOCKED", blocked.ErrorCode);
        Assert.Contains("1 still open", blocked.Message);
    }

    [Fact]
    public async Task Approve_FrameworkFailure_ReturnsCheckPayload()
    {
        PinFramework(minModules: 5);

        var failed = await Assert.ThrowsAsync<ConflictException>(() => Sut(_advisorUserId).ApproveAsync(
            _programId,
            new ApproveProgramRequest { CurriculumVersion = 3 }));

        Assert.Equal("FRAMEWORK_CHECK_FAILED", failed.ErrorCode);
        var check = Assert.IsType<FrameworkCheckDto>(failed.Payload);
        Assert.False(check.AllPassed);
        Assert.Contains(check.Checks, c => c.Code == "MinModules" && !c.Passed);
    }

    [Fact]
    public async Task Approve_Success_CreatesApprovalResolvesAddressedPinsAndNotifies()
    {
        PinFramework(minModules: 1);
        var addressed = SeedPin(DiscussionPinStatus.Addressed, 1);
        SeedApproval(version: 1, revokedAt: _now.AddHours(-2));

        var workspace = await Sut(_advisorUserId).ApproveAsync(
            _programId,
            new ApproveProgramRequest { CurriculumVersion = 3, Comment = "  Looks good  " });

        Assert.Equal(ProgramStatus.Approved, workspace.Status);
        Assert.Equal(ProgramStatus.Approved, TheProgram.Status);
        var approval = _db.ProgramApprovals.Items.Single(a => a.RevokedAt == null);
        Assert.Equal(3, approval.CurriculumVersion);
        Assert.Equal(1, approval.FromVersion);
        Assert.Equal(_advisorExpertId, approval.ApprovedByExpertId);
        Assert.Equal("Looks good", approval.Comment);
        Assert.Equal(_frameworkVersionId, approval.FrameworkVersionId);
        Assert.Equal(DiscussionPinStatus.Resolved, addressed.PinStatus);
        Assert.Equal(_advisorUserId, addressed.ResolvedByUserId);
        Assert.Equal(DiscussionSystemEventCode.Approved, LastSystemMessage().SystemEventCode);
        Assert.NotNull(workspace.Approval);
        Assert.Equal(approval.Id, workspace.Approval!.Id);
        Assert.Equal("EXP-ADV", workspace.Approval.ApprovedByName);
        var notification = Assert.Single(_published);
        Assert.Equal(NotificationType.CurriculumReviewApproved, notification.Type);
    }

    [Fact]
    public async Task Revoke_ByManager_ReopensAndNotifiesAdvisor()
    {
        TheProgram.Status = ProgramStatus.Approved;
        var approval = SeedApproval(version: 3);

        var workspace = await Sut(_managerId).RevokeAsync(
            _programId,
            new RevokeProgramApprovalRequest { Reason = "Need another module" });

        Assert.Equal(ProgramStatus.Draft, workspace.Status);
        Assert.Equal(_now, approval.RevokedAt);
        Assert.Equal(ProgramApprovalRevokeReason.ManagerReopened, approval.RevokeReason);
        Assert.Equal("Need another module", approval.RevokeComment);
        Assert.Equal(_managerId, approval.RevokedByUserId);
        Assert.Equal(DiscussionSystemEventCode.ApprovalRevoked, LastSystemMessage().SystemEventCode);
        var notification = Assert.Single(_published);
        Assert.Equal(NotificationType.CurriculumApprovalRevoked, notification.Type);
        Assert.Equal(NotificationAudienceKind.User, notification.Audience.Kind);
        Assert.Equal(_advisorUserId, notification.Audience.UserId);
    }

    [Fact]
    public async Task Revoke_ByAdvisor_NotifiesManagers()
    {
        TheProgram.Status = ProgramStatus.Approved;
        var approval = SeedApproval(version: 3);

        await Sut(_advisorUserId).RevokeAsync(_programId, null);

        Assert.Equal(ProgramStatus.Draft, TheProgram.Status);
        Assert.Equal(ProgramApprovalRevokeReason.ExpertRevoked, approval.RevokeReason);
        Assert.Null(approval.RevokeComment);
        var notification = Assert.Single(_published);
        Assert.Equal(NotificationType.CurriculumApprovalRevoked, notification.Type);
        Assert.Equal(NotificationAudienceKind.Managers, notification.Audience.Kind);
    }

    [Fact]
    public async Task Revoke_RejectsBoardExpertAndNonApproved()
    {
        TheProgram.Status = ProgramStatus.Approved;
        SeedApproval(version: 3);
        await Assert.ThrowsAsync<ForbiddenException>(() => Sut(_boardUserId).RevokeAsync(_programId, null));

        TheProgram.Status = ProgramStatus.Draft;
        var status = await Assert.ThrowsAsync<ConflictException>(() => Sut(_managerId).RevokeAsync(_programId, null));
        Assert.Equal("INVALID_STATUS", status.ErrorCode);
    }

    [Fact]
    public async Task Publish_RequiresApprovalAtCurrentVersion()
    {
        TheProgram.Status = ProgramStatus.Approved;
        SeedApproval(version: 2);

        var stale = await Assert.ThrowsAsync<ConflictException>(() => Sut(_managerId).PublishAsync(_programId));
        Assert.Equal("CURRICULUM_VERSION_STALE", stale.ErrorCode);

        _db.ProgramApprovals.Items.Single().CurriculumVersion = 3;
        await Assert.ThrowsAsync<ForbiddenException>(() => Sut(_advisorUserId).PublishAsync(_programId));

        await Sut(_managerId).PublishAsync(_programId);

        Assert.Equal(ProgramStatus.Active, TheProgram.Status);
        Assert.Equal(DiscussionSystemEventCode.Published, LastSystemMessage().SystemEventCode);
        var notification = Assert.Single(_published);
        Assert.Equal(NotificationType.CurriculumReviewPublished, notification.Type);
    }

    [Fact]
    public async Task AssignAdvisor_Draft_AddsBoardRowAndPostsAdvisorChanged()
    {
        await Sut(_managerId).AssignAdvisorAsync(
            _programId,
            new AssignProgramAdvisorRequest { AdvisorExpertId = _boardExpertId });

        Assert.Equal(_boardExpertId, TheProgram.AdvisorExpertId);
        Assert.Equal(ProgramStatus.Draft, TheProgram.Status);
        Assert.Single(_db.ProgramBoards.Items, b => b.ExpertId == _boardExpertId);
        Assert.Equal(DiscussionSystemEventCode.AdvisorChanged, LastSystemMessage().SystemEventCode);
        Assert.Empty(_db.ProgramApprovals.Items);
    }

    [Fact]
    public async Task AssignAdvisor_Approved_RevokesWithAdvisorChangedWithoutNotification()
    {
        TheProgram.Status = ProgramStatus.Approved;
        var approval = SeedApproval(version: 3);

        await Sut(_managerId).AssignAdvisorAsync(
            _programId,
            new AssignProgramAdvisorRequest { AdvisorExpertId = _boardExpertId });

        Assert.Equal(ProgramStatus.Draft, TheProgram.Status);
        Assert.Equal(ProgramApprovalRevokeReason.AdvisorChanged, approval.RevokeReason);
        Assert.Contains(
            _db.ProgramAdvisoryDiscussionMessages.Items,
            m => m.SystemEventCode == DiscussionSystemEventCode.AdvisorChanged);
        Assert.Equal(DiscussionSystemEventCode.ApprovalRevoked, LastSystemMessage().SystemEventCode);
        Assert.Empty(_published);
    }

    // ── Realtime ──────────────────────────────────────────────────────────────

    private List<(string Scope, object? Payload)> SyncEvents()
    {
        Assert.All(_sync.Events, e =>
        {
            Assert.Equal(NotificationAudienceKind.AdvisoryParticipants, e.Audience.Kind);
            Assert.Equal(_programId, e.Audience.ProgramId);
            Assert.Equal(_programId, e.EntityId);
        });
        return _sync.Events.Select(e => (e.Scope, e.Payload)).ToList();
    }

    [Fact]
    public async Task Approve_PublishesResolvedPinsApprovalAndDiscussionChanges()
    {
        PinFramework(minModules: 1);
        var addressed = SeedPin(DiscussionPinStatus.Addressed, 1);
        SeedPin(DiscussionPinStatus.Resolved, 2);

        await Sut(_advisorUserId).ApproveAsync(_programId, new ApproveProgramRequest { CurriculumVersion = 3 });

        Assert.Equal(
            [
                (SyncScopes.AdvisoryPinChanged, (object?)new AdvisoryPinChangedPayload { MessageId = addressed.Id }),
                (SyncScopes.AdvisoryApprovalChanged, new AdvisoryApprovalChangedPayload { Status = "Approved", CurriculumVersion = 3 }),
                (SyncScopes.AdvisoryDiscussionChanged, new AdvisoryDiscussionChangedPayload { LatestSequence = 3 }),
            ],
            SyncEvents());
    }

    [Fact]
    public async Task RequestRevokeAndPublish_PublishApprovalChangedWithStatus()
    {
        await Sut(_managerId).RequestApprovalAsync(_programId);
        Assert.Contains(
            (SyncScopes.AdvisoryApprovalChanged, (object?)new AdvisoryApprovalChangedPayload { Status = "Draft", CurriculumVersion = 3 }),
            SyncEvents());

        _sync.Events.Clear();
        TheProgram.Status = ProgramStatus.Approved;
        SeedApproval(version: 3);
        await Sut(_managerId).PublishAsync(_programId);
        Assert.Contains(
            (SyncScopes.AdvisoryApprovalChanged, (object?)new AdvisoryApprovalChangedPayload { Status = "Active", CurriculumVersion = 3 }),
            SyncEvents());

        _sync.Events.Clear();
        TheProgram.Status = ProgramStatus.Approved;
        await Sut(_advisorUserId).RevokeAsync(_programId, null);
        Assert.Equal(
            [
                (SyncScopes.AdvisoryApprovalChanged, (object?)new AdvisoryApprovalChangedPayload { Status = "Draft", CurriculumVersion = 3 }),
                (SyncScopes.AdvisoryDiscussionChanged, new AdvisoryDiscussionChangedPayload { LatestSequence = 3 }),
            ],
            SyncEvents());
    }

    [Fact]
    public async Task AssignAdvisor_PublishesDiscussionChangedOnly_WhenDraft()
    {
        await Sut(_managerId).AssignAdvisorAsync(
            _programId,
            new AssignProgramAdvisorRequest { AdvisorExpertId = _advisorExpertId });
        Assert.Empty(_sync.Events);

        await Sut(_managerId).AssignAdvisorAsync(
            _programId,
            new AssignProgramAdvisorRequest { AdvisorExpertId = _boardExpertId });

        Assert.Equal(
            [(SyncScopes.AdvisoryDiscussionChanged, (object?)new AdvisoryDiscussionChangedPayload { LatestSequence = 1 })],
            SyncEvents());
    }

    [Fact]
    public async Task FailedLifecycleAction_PublishesNothing()
    {
        SeedPin(DiscussionPinStatus.Open, 1);

        await Assert.ThrowsAsync<ConflictException>(() => Sut(_advisorUserId).ApproveAsync(
            _programId,
            new ApproveProgramRequest { CurriculumVersion = 3 }));
        await Assert.ThrowsAsync<ConflictException>(() => Sut(_managerId).RevokeAsync(_programId, null));

        Assert.Empty(_sync.Events);
    }

    [Fact]
    public async Task AssignAdvisor_Active_Conflict()
    {
        TheProgram.Status = ProgramStatus.Active;

        var ex = await Assert.ThrowsAsync<ConflictException>(() => Sut(_managerId).AssignAdvisorAsync(
            _programId,
            new AssignProgramAdvisorRequest { AdvisorExpertId = _boardExpertId }));

        Assert.Equal("INVALID_STATUS", ex.ErrorCode);
        Assert.Equal(_advisorExpertId, TheProgram.AdvisorExpertId);
    }

    [Fact]
    public async Task Workspace_ReportsParticipantsCapabilitiesAndCounts()
    {
        SeedPin(DiscussionPinStatus.Open, 1);
        SeedPin(DiscussionPinStatus.Addressed, 2);
        SeedPin(DiscussionPinStatus.Resolved, 3);
        _db.ProgramAdvisoryStreamReads.Seed(new ProgramAdvisoryStreamRead
        {
            Id = Guid.NewGuid(),
            ProgramId = _programId,
            UserId = _managerId,
            StreamType = AdvisoryStreamType.Discussion,
            LastReadSequence = 1,
        });
        SeedApproval(version: 1, revokedAt: _now.AddHours(-1));
        SeedChange(2, Guid.NewGuid());
        SeedChange(3, Guid.NewGuid());
        _db.CurriculumChangeSeens.Seed(new CurriculumChangeSeen
        {
            Id = Guid.NewGuid(),
            ProgramId = _programId,
            UserId = _managerId,
            SeenVersion = 2,
        });

        var manager = await Sut(_managerId).GetWorkspaceAsync(_programId);

        Assert.Equal(3, manager.CurriculumVersion);
        Assert.Equal("EXP-ADV", manager.AdvisorName);
        Assert.Null(manager.Approval);
        Assert.Equal(1, manager.OpenPinCount);
        Assert.Equal(1, manager.AddressedPinCount);
        Assert.Equal(2, manager.UnreadCount);
        Assert.Equal(3, manager.LatestSequence);
        Assert.True(manager.FrameworkCheckPassed);
        Assert.Equal(2, manager.ChangesSinceApprovalCount);
        Assert.Equal(1, manager.UnseenChangeCount);
        Assert.Equal(
            [_managerId, _advisorUserId, _boardUserId],
            manager.Participants.Select(p => p.UserId).ToArray());
        Assert.True(manager.Participants.Single(p => p.UserId == _advisorUserId).IsAdvisor);
        Assert.True(manager.Capabilities.CanPost);
        Assert.False(manager.Capabilities.CanPin);
        Assert.True(manager.Capabilities.CanEditCurriculum);
        Assert.True(manager.Capabilities.CanRequestApproval);
        Assert.False(manager.Capabilities.CanApprove);
        Assert.False(manager.Capabilities.CanPublish);

        var advisor = await Sut(_advisorUserId).GetWorkspaceAsync(_programId);
        Assert.True(advisor.Capabilities.CanPin);
        Assert.True(advisor.Capabilities.CanApprove);
        Assert.False(advisor.Capabilities.CanEditCurriculum);
        Assert.Equal(2, advisor.UnseenChangeCount);

        var board = await Sut(_boardUserId).GetWorkspaceAsync(_programId);
        Assert.True(board.Capabilities.CanResolvePin);
        Assert.False(board.Capabilities.CanApprove);
        Assert.False(board.Capabilities.CanRevokeApproval);
    }

    [Fact]
    public async Task Workspace_ApprovedAtCurrentVersion_AllowsPublishAndRevoke()
    {
        TheProgram.Status = ProgramStatus.Approved;
        SeedApproval(version: 3);

        var manager = await Sut(_managerId).GetWorkspaceAsync(_programId);

        Assert.True(manager.Capabilities.CanPublish);
        Assert.True(manager.Capabilities.CanRevokeApproval);
        Assert.False(manager.Capabilities.CanRequestApproval);
        Assert.NotNull(manager.Approval);
        Assert.Equal(0, manager.ChangesSinceApprovalCount);

        var advisor = await Sut(_advisorUserId).GetWorkspaceAsync(_programId);
        Assert.True(advisor.Capabilities.CanRevokeApproval);
        Assert.False(advisor.Capabilities.CanApprove);
    }
}
