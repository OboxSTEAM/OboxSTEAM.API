using System.Text.Json;
using Moq;
using OboxSteam.Application.Commons.CurriculumChanges;
using OboxSteam.Application.Interfaces;
using OboxSteam.Application.Notifications;
using OboxSteam.Application.Services;
using OboxSteam.Domain.Entities;
using OboxSteam.Domain.Enums;
using OboxSteam.Test.Helpers;

namespace OboxSteam.Test.UnitTests;

public sealed class CurriculumChangeRecorderTests
{
    private readonly InMemoryUnitOfWork _db = new();
    private readonly Mock<IClaimsService> _claims = new();
    private readonly Mock<ICurrentTime> _time = new();
    private readonly Mock<INotificationPublisher> _notifications = new();
    private readonly Guid _managerId = Guid.NewGuid();
    private readonly Guid _advisorUserId = Guid.NewGuid();
    private DateTime _now = new(2026, 9, 30, 10, 0, 0, DateTimeKind.Utc);

    private readonly Program _program;
    private readonly Module _module;
    private readonly Course _course;
    private readonly Activity _activity;

    public CurriculumChangeRecorderTests()
    {
        _claims.SetupGet(c => c.GetCurrentUserId).Returns(_managerId);
        _time.Setup(t => t.GetCurrentTime()).Returns(() => _now);
        _db.Users.Seed(new User
        {
            Id = _managerId, Code = "USR-M", Email = "manager@test.local", FullName = "Lan Nguyen",
            Role = RoleType.Manager, Status = AccountStatus.Active,
        });
        _program = new Program { Id = Guid.NewGuid(), Code = "PRG", Name = "Robotics", Status = ProgramStatus.Draft };
        _module = new Module { Id = Guid.NewGuid(), Code = "M1", Name = "Module 1", ProgramId = _program.Id, ModuleOrder = 1 };
        _course = new Course { Id = Guid.NewGuid(), Code = "C1", Name = "Course 1", ModuleId = _module.Id, CourseOrder = 1 };
        _activity = new Activity
        {
            Id = Guid.NewGuid(), Code = "A1", Name = "Volcano lab", CourseId = _course.Id,
            ActivityOrder = 1, DurationMinutes = 45, ActivityType = ActivityType.Offline,
        };
        _db.Programs.Seed(_program);
        _db.Modules.Seed(_module);
        _db.Courses.Seed(_course);
        _db.Activities.Seed(_activity);
    }

    private CurriculumChangeRecorder CreateSut() => new(_db, _claims.Object, _time.Object, _notifications.Object);

    private ProgramApproval SeedApprovalWithAdvisor(bool seedApproval = true)
    {
        var expert = new Expert { Id = Guid.NewGuid(), Code = "EXP", FullName = "Dr. Minh", UserId = _advisorUserId };
        _db.Experts.Seed(expert);
        _program.AdvisorExpertId = expert.Id;
        var approval = new ProgramApproval { Id = Guid.NewGuid(), ProgramId = _program.Id, ApprovedAt = _now.AddHours(-1) };
        if (seedApproval)
        {
            _db.ProgramApprovals.Seed(approval);
        }

        return approval;
    }

    private void VerifyAdvisorNotified(Times times)
        => _notifications.Verify(
            n => n.PublishManyAsync(
                It.Is<IReadOnlyList<NotificationCommand>>(c => c.Count == 1
                    && c[0].Type == NotificationType.CurriculumApprovalRevoked
                    && c[0].Audience.UserId == _advisorUserId),
                It.IsAny<CancellationToken>()),
            times);

    private static CurriculumEntryChange Modified(object entity, params (string Name, object? Value)[] originals)
        => new(entity, CurriculumEntryState.Modified, originals.ToDictionary(o => o.Name, o => o.Value));

    private static CurriculumEntryChange Added(object entity)
        => new(entity, CurriculumEntryState.Added, new Dictionary<string, object?>());

    private List<ProgramAdvisoryDiscussionMessage> SessionMessages()
        => _db.ProgramAdvisoryDiscussionMessages.Items
            .Where(m => m.SystemEventCode == DiscussionSystemEventCode.CurriculumUpdated)
            .ToList();

    private static CurriculumUpdatedPayload Payload(ProgramAdvisoryDiscussionMessage message)
        => JsonSerializer.Deserialize<CurriculumUpdatedPayload>(message.SystemEventPayloadJson!, CurriculumChangeJson.Options)!;

    [Fact]
    public async Task Record_FieldEdit_BumpsVersionAndWritesRowWithPath()
    {
        await CreateSut().RecordAsync([Modified(_activity, (nameof(Activity.DurationMinutes), 90))]);

        Assert.Equal(1, _program.CurriculumVersion);
        var row = Assert.Single(_db.CurriculumChanges.Items);
        Assert.Equal(1, row.Version);
        Assert.Equal(ProgramAdvisoryTargetType.Activity, row.TargetType);
        Assert.Equal(CurriculumChangeKind.Updated, row.ChangeKind);
        Assert.Equal("Lan Nguyen", row.ActorName);
        var field = Assert.Single(CurriculumChangeJson.DeserializeFields(row.FieldsJson));
        Assert.Equal("durationMinutes", field.FieldKey);
        Assert.Equal(90, field.Before!.GetValue<int>());
        Assert.Equal(45, field.After!.GetValue<int>());
        Assert.Equal(
            ["Robotics", "Module 1", "Course 1"],
            CurriculumChangeJson.DeserializePath(row.PathSnapshotJson).Select(s => s.Label));
    }

    [Fact]
    public async Task Record_NonCurriculumField_IsIgnored()
    {
        _program.Price = 200;

        await CreateSut().RecordAsync([Modified(_program, (nameof(Program.Price), 100m))]);

        Assert.Equal(0, _program.CurriculumVersion);
        Assert.Empty(_db.CurriculumChanges.Items);
        Assert.Empty(_db.ProgramAdvisoryDiscussionMessages.Items);
    }

    [Theory]
    [InlineData(ProgramStatus.Active)]
    [InlineData(ProgramStatus.Inactive)]
    public async Task Record_PublishedProgram_RevokesApprovalAndKeepsStatus(ProgramStatus status)
    {
        _program.Status = status;
        var approval = SeedApprovalWithAdvisor();
        var sut = CreateSut();

        await sut.RecordAsync([Modified(_activity, (nameof(Activity.DurationMinutes), 90))]);

        Assert.Equal(status, _program.Status);
        Assert.Equal(1, _program.CurriculumVersion);
        Assert.Single(_db.CurriculumChanges.Items);
        Assert.Equal(_now, approval.RevokedAt);
        await sut.FlushNotificationsAsync();
        VerifyAdvisorNotified(Times.Once());
    }

    [Fact]
    public async Task Record_ApprovedProgram_RevokesApprovalAndReturnsToDraft()
    {
        _program.Status = ProgramStatus.Approved;
        var approval = SeedApprovalWithAdvisor();

        await CreateSut().RecordAsync([Modified(_activity, (nameof(Activity.DurationMinutes), 90))]);

        Assert.Equal(ProgramStatus.Draft, _program.Status);
        Assert.Equal(_now, approval.RevokedAt);
        Assert.Equal(ProgramApprovalRevokeReason.CurriculumEdited, approval.RevokeReason);
        Assert.Equal(_managerId, approval.RevokedByUserId);
        Assert.Contains(
            _db.ProgramAdvisoryDiscussionMessages.Items,
            m => m.SystemEventCode == DiscussionSystemEventCode.ApprovalRevoked && m.Kind == DiscussionMessageKind.System);
    }

    [Fact]
    public async Task Record_NotifiesAdvisorOnlyOnFirstEditAfterApproval()
    {
        _program.Status = ProgramStatus.Approved;
        SeedApprovalWithAdvisor();
        var sut = CreateSut();

        await sut.RecordAsync([Modified(_activity, (nameof(Activity.DurationMinutes), 90))]);
        await sut.FlushNotificationsAsync();
        _now = _now.AddMinutes(1);
        await sut.RecordAsync([Modified(_activity, (nameof(Activity.DurationMinutes), 45))]);
        await sut.FlushNotificationsAsync();

        VerifyAdvisorNotified(Times.Once());
        Assert.Single(
            _db.ProgramAdvisoryDiscussionMessages.Items,
            m => m.SystemEventCode == DiscussionSystemEventCode.ApprovalRevoked);
    }

    [Fact]
    public async Task Record_NotificationIsNotPublishedBeforeFlush()
    {
        _program.Status = ProgramStatus.Approved;
        SeedApprovalWithAdvisor();

        await CreateSut().RecordAsync([Modified(_activity, (nameof(Activity.DurationMinutes), 90))]);

        _notifications.Verify(
            n => n.PublishManyAsync(It.IsAny<IReadOnlyList<NotificationCommand>>(), It.IsAny<CancellationToken>()),
            Times.Never());
    }

    [Fact]
    public async Task Discard_DropsQueuedNotification()
    {
        _program.Status = ProgramStatus.Approved;
        SeedApprovalWithAdvisor();
        var sut = CreateSut();

        await sut.RecordAsync([Modified(_activity, (nameof(Activity.DurationMinutes), 90))]);
        sut.DiscardNotifications();
        await sut.FlushNotificationsAsync();

        VerifyAdvisorNotified(Times.Never());
    }

    [Fact]
    public async Task Record_DraftWithoutApproval_DoesNotNotify()
    {
        SeedApprovalWithAdvisor(seedApproval: false);
        var sut = CreateSut();

        await sut.RecordAsync([Modified(_activity, (nameof(Activity.DurationMinutes), 90))]);
        await sut.FlushNotificationsAsync();

        Assert.Equal(ProgramStatus.Draft, _program.Status);
        VerifyAdvisorNotified(Times.Never());
    }

    [Fact]
    public async Task Record_Suppressed_DoesNothing()
    {
        using (CurriculumChangeScope.Suppress())
        {
            await CreateSut().RecordAsync([Modified(_activity, (nameof(Activity.DurationMinutes), 90))]);
        }

        Assert.Equal(0, _program.CurriculumVersion);
        Assert.Empty(_db.CurriculumChanges.Items);
    }

    [Fact]
    public async Task Record_ChildrenOfProgramBeingCreated_AreSkipped()
    {
        var program = new Program { Id = Guid.NewGuid(), Code = "NEW", Name = "New" };
        var module = new Module { Id = Guid.NewGuid(), Code = "NM", Name = "New module", ProgramId = program.Id };

        await CreateSut().RecordAsync([Added(program), Added(module)]);

        Assert.Empty(_db.CurriculumChanges.Items);
    }

    [Fact]
    public async Task Record_CascadedDelete_RecordsOnlyTopmostComponent()
    {
        _course.IsDeleted = true;
        _activity.IsDeleted = true;

        await CreateSut().RecordAsync(
        [
            Modified(_course, (nameof(BaseEntity.IsDeleted), false)),
            Modified(_activity, (nameof(BaseEntity.IsDeleted), false)),
        ]);

        var row = Assert.Single(_db.CurriculumChanges.Items);
        Assert.Equal(_course.Id, row.TargetId);
        Assert.Equal(CurriculumChangeKind.Deleted, row.ChangeKind);
    }

    [Fact]
    public async Task Record_InsertShiftingSiblings_RecordsOnlyCreated()
    {
        var inserted = new Activity
        {
            Id = Guid.NewGuid(), Code = "A0", Name = "Warm-up", CourseId = _course.Id, ActivityOrder = 1,
        };
        _activity.ActivityOrder = 2;

        await CreateSut().RecordAsync(
        [
            Added(inserted),
            Modified(_activity, (nameof(Activity.ActivityOrder), 1)),
        ]);

        var row = Assert.Single(_db.CurriculumChanges.Items);
        Assert.Equal(inserted.Id, row.TargetId);
        Assert.Equal(CurriculumChangeKind.Created, row.ChangeKind);
        Assert.Equal(_course.Id, row.ParentAfter);
    }

    [Fact]
    public async Task Record_SkillLink_RecordsProgramFieldWithSkillName()
    {
        var skill = new Skill { Id = Guid.NewGuid(), Code = "SK", Name = "Soldering" };
        _db.Skills.Seed(skill);

        await CreateSut().RecordAsync([Added(new ProgramSkill { Id = Guid.NewGuid(), ProgramId = _program.Id, SkillId = skill.Id })]);

        var row = Assert.Single(_db.CurriculumChanges.Items);
        Assert.Equal(ProgramAdvisoryTargetType.Program, row.TargetType);
        var field = Assert.Single(CurriculumChangeJson.DeserializeFields(row.FieldsJson));
        Assert.Equal($"skill:{skill.Id}", field.FieldKey);
        Assert.Equal("Soldering", field.Label);
        Assert.False(field.Before!.GetValue<bool>());
        Assert.True(field.After!.GetValue<bool>());
    }

    [Fact]
    public async Task Record_SecondEditInsideWindow_ExtendsSessionMessage()
    {
        var sut = CreateSut();
        await sut.RecordAsync([Modified(_activity, (nameof(Activity.DurationMinutes), 90))]);
        var first = Assert.Single(SessionMessages());
        Assert.Equal(1, first.Sequence);

        _now = _now.AddMinutes(5);
        _activity.Name = "Volcano lab v2";
        await sut.RecordAsync([Modified(_activity, (nameof(Activity.Name), "Volcano lab"))]);

        var message = Assert.Single(SessionMessages());
        var payload = Payload(message);
        Assert.Equal(0, payload.FromVersion);
        Assert.Equal(2, payload.ToVersion);
        Assert.Equal(1, payload.ChangeCount);
        Assert.Equal(_now, message.EditedAt);
        Assert.Equal(2, message.Sequence);
    }

    [Fact]
    public async Task Record_AfterIdleWindow_PostsNewSessionMessage()
    {
        var sut = CreateSut();
        await sut.RecordAsync([Modified(_activity, (nameof(Activity.DurationMinutes), 90))]);

        _now = _now.Add(CurriculumChangeRecorder.SessionIdleWindow).AddSeconds(1);
        _activity.Name = "Renamed";
        await sut.RecordAsync([Modified(_activity, (nameof(Activity.Name), "Volcano lab"))]);

        var messages = SessionMessages().OrderBy(m => m.Sequence).ToList();
        Assert.Equal(2, messages.Count);
        Assert.Equal(1, Payload(messages[1]).FromVersion);
        Assert.Equal(2, Payload(messages[1]).ToVersion);
    }

    [Fact]
    public async Task Record_AfterActorPostedMessage_PostsNewSessionMessage()
    {
        var sut = CreateSut();
        await sut.RecordAsync([Modified(_activity, (nameof(Activity.DurationMinutes), 90))]);
        _db.ProgramAdvisoryDiscussionMessages.Seed(new ProgramAdvisoryDiscussionMessage
        {
            Id = Guid.NewGuid(), ProgramId = _program.Id, AuthorUserId = _managerId, Sequence = 2,
            Kind = DiscussionMessageKind.User, Text = "Updated the lab", ClientMessageId = "c-1", CreatedAt = _now,
        });
        _program.AdvisoryDiscussionSequence = 2;

        _now = _now.AddMinutes(1);
        _activity.Name = "Renamed";
        await sut.RecordAsync([Modified(_activity, (nameof(Activity.Name), "Volcano lab"))]);

        Assert.Equal(2, SessionMessages().Count);
    }
}
