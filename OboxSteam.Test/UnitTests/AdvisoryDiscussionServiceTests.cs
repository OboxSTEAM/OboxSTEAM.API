using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OboxSteam.Application.Commons;
using OboxSteam.Application.DTOs.MaterialDTO;
using OboxSteam.Application.DTOs.ProgramAdvisoryDTO;
using OboxSteam.Application.Exceptions;
using OboxSteam.Application.Interfaces;
using OboxSteam.Application.Notifications;
using OboxSteam.Application.Services;
using OboxSteam.Domain.Entities;
using OboxSteam.Domain.Enums;
using OboxSteam.Test.Helpers;

namespace OboxSteam.Test.UnitTests;

public sealed class AdvisoryDiscussionServiceTests
{
    private readonly Guid _managerId = Guid.Parse("13131313-1313-1313-1313-131313131313");
    private readonly Guid _advisorUserId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private readonly Guid _boardUserId = Guid.Parse("12121212-1212-1212-1212-121212121212");
    private readonly Guid _outsiderUserId = Guid.Parse("14141414-1414-1414-1414-141414141414");
    private readonly Guid _advisorExpertId = Guid.Parse("66666666-6666-6666-6666-666666666666");
    private readonly Guid _boardExpertId = Guid.Parse("67676767-6767-6767-6767-676767676767");
    private readonly Guid _outsiderExpertId = Guid.Parse("68686868-6868-6868-6868-686868686868");
    private readonly Guid _programId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private readonly Guid _moduleId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private readonly Guid _courseId = Guid.Parse("44444444-4444-4444-4444-444444444444");
    private readonly Guid _activityWithMaterialId = Guid.Parse("55555555-5555-5555-5555-555555555555");
    private readonly Guid _emptyActivityId = Guid.Parse("56565656-5656-5656-5656-565656565656");
    private readonly Guid _materialId = Guid.Parse("77777777-7777-7777-7777-777777777777");
    private readonly Guid _assignmentId = Guid.Parse("88888888-8888-8888-8888-888888888888");
    private readonly DateTime _now = new(2026, 10, 1, 12, 0, 0, DateTimeKind.Utc);

    private readonly InMemoryUnitOfWork _db = new();
    private readonly Mock<IClaimsService> _claimsService = new();
    private readonly Mock<ICurrentTime> _currentTime = new();
    private readonly Mock<IBlobService> _blobService = new();

    public AdvisoryDiscussionServiceTests()
    {
        _currentTime.Setup(c => c.GetCurrentTime()).Returns(() => _now);
        _blobService
            .Setup(b => b.GetFileUrlAsync(It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string key, TimeSpan _, CancellationToken _) => $"https://signed.test/{key}");
        _blobService
            .Setup(b => b.UploadFileAsync(It.IsAny<string>(), It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _blobService
            .Setup(b => b.DeleteByKeyAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _blobService
            .Setup(b => b.CopyObjectAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _blobService
            .Setup(b => b.GetPreviewUrlAsync(It.IsAny<string>()))
            .ReturnsAsync((string key) => $"https://cdn.test/{key}");
        SeedBase();
    }

    private ProgramAdvisoryDiscussionService Chat(Guid userId)
    {
        _claimsService.Setup(c => c.GetCurrentUserId).Returns(userId);
        return new ProgramAdvisoryDiscussionService(
            _db,
            _claimsService.Object,
            _currentTime.Object,
            new AdvisoryReferenceResolver(_db, _currentTime.Object));
    }

    private ProgramAdvisoryAttachmentService Attachments(Guid userId)
    {
        _claimsService.Setup(c => c.GetCurrentUserId).Returns(userId);
        return new ProgramAdvisoryAttachmentService(
            _db,
            _claimsService.Object,
            _currentTime.Object,
            _blobService.Object,
            NullLogger<ProgramAdvisoryAttachmentService>.Instance);
    }

    private MaterialService Materials(Guid userId)
    {
        _claimsService.Setup(c => c.GetCurrentUserId).Returns(userId);
        var publisher = new Mock<INotificationPublisher>();
        publisher
            .Setup(n => n.PublishManyAsync(It.IsAny<IReadOnlyList<NotificationCommand>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        return new MaterialService(
            _claimsService.Object,
            _db,
            _blobService.Object,
            Mock.Of<IEnrollmentCurriculumService>(),
            publisher.Object,
            NullLogger<MaterialService>.Instance);
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

    private void SeedExpert(Guid expertId, Guid userId, string code)
        => _db.Experts.Seed(new Expert { Id = expertId, Code = code, FullName = code, UserId = userId });

    private void SeedBase()
    {
        SeedUser(_managerId, RoleType.Manager, "USR-MGR");
        SeedUser(_advisorUserId, RoleType.Expert, "USR-ADV");
        SeedUser(_boardUserId, RoleType.Expert, "USR-BRD");
        SeedUser(_outsiderUserId, RoleType.Expert, "USR-OUT");
        SeedExpert(_advisorExpertId, _advisorUserId, "EXP-ADV");
        SeedExpert(_boardExpertId, _boardUserId, "EXP-BRD");
        SeedExpert(_outsiderExpertId, _outsiderUserId, "EXP-OUT");
        _db.Programs.Seed(new Program
        {
            Id = _programId,
            Code = "PRG-001",
            Name = "Robotics",
            Category = ProgramCategory.Technology,
            Level = DifficultyLevel.Beginner,
            Status = ProgramStatus.Draft,
            AdvisorExpertId = _advisorExpertId,
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
        _db.Programs.Items.Single().Modules = [module];
        _db.Courses.Seed(new Course { Id = _courseId, ModuleId = _moduleId, Code = "CRS-001", Name = "Basics" });
        _db.Activities.Seed(new Activity
        {
            Id = _activityWithMaterialId,
            CourseId = _courseId,
            Code = "ACT-001",
            Name = "Lesson",
            ActivityType = ActivityType.SelfPaced,
            ActivityOrder = 1,
        });
        _db.Activities.Seed(new Activity
        {
            Id = _emptyActivityId,
            CourseId = _courseId,
            Code = "ACT-002",
            Name = "Reading",
            ActivityType = ActivityType.SelfPaced,
            ActivityOrder = 2,
        });
        _db.Materials.Seed(new Material
        {
            Id = _materialId,
            ActivityId = _activityWithMaterialId,
            Title = "Slides",
            MaterialType = MaterialType.PDF,
            FileUrl = "https://cdn.test/materials/pdf/slides.pdf",
            FileSizeBytes = 1024,
        });
        _db.Assignments.Seed(new Assignment
        {
            Id = _assignmentId,
            ModuleId = _moduleId,
            CourseId = _courseId,
            Code = "ASG-001",
            Title = "Build a robot",
        });
    }

    private static PostAdvisoryDiscussionMessageRequest Post(string? text, string clientId, List<Guid>? attachmentIds = null)
        => new() { Text = text, ClientMessageId = clientId, AttachmentIds = attachmentIds };

    private static string Token(ProgramAdvisoryTargetType type, Guid id) => AdvisoryMentionTokens.Format(type, id);

    private ProgramAdvisoryDiscussionAttachment SeedAttachment(
        Guid uploaderId,
        Guid? messageId = null,
        string fileName = "notes.pdf",
        long size = 2048,
        DateTime? createdAt = null)
    {
        var attachment = new ProgramAdvisoryDiscussionAttachment
        {
            Id = Guid.NewGuid(),
            ProgramId = _programId,
            UploaderUserId = uploaderId,
            MessageId = messageId,
            FileName = fileName,
            ContentType = "application/pdf",
            SizeBytes = size,
            Kind = DiscussionAttachmentKind.File,
            StorageKey = $"advisory/{_programId}/{Guid.NewGuid()}/{fileName}",
            CreatedAt = createdAt ?? _now,
        };
        _db.ProgramAdvisoryDiscussionAttachments.Seed(attachment);
        return attachment;
    }

    private static Mock<IFormFile> File(string fileName, long length, string contentType)
    {
        var file = new Mock<IFormFile>();
        file.Setup(f => f.FileName).Returns(fileName);
        file.Setup(f => f.Length).Returns(length);
        file.Setup(f => f.ContentType).Returns(contentType);
        file.Setup(f => f.OpenReadStream()).Returns(() => new MemoryStream([1, 2, 3]));
        return file;
    }

    // ── Mention targets ───────────────────────────────────────────────────────

    [Fact]
    public async Task MentionTargets_AreFlattenedInCurriculumOrder_WithPaths()
    {
        var targets = await Chat(_boardUserId).GetMentionTargetsAsync(_programId);

        Assert.Equal(
            [
                (ProgramAdvisoryTargetType.Program, _programId),
                (ProgramAdvisoryTargetType.Module, _moduleId),
                (ProgramAdvisoryTargetType.Course, _courseId),
                (ProgramAdvisoryTargetType.Activity, _activityWithMaterialId),
                (ProgramAdvisoryTargetType.Material, _materialId),
                (ProgramAdvisoryTargetType.Activity, _emptyActivityId),
                (ProgramAdvisoryTargetType.Assignment, _assignmentId),
            ],
            targets.Select(t => (t.TargetType, t.TargetId)).ToList());
        var material = targets.Single(t => t.TargetType == ProgramAdvisoryTargetType.Material);
        Assert.Equal(_activityWithMaterialId, material.ActivityId);
        Assert.Equal(4, material.Path.Count);
        Assert.Null(targets.Single(t => t.TargetType == ProgramAdvisoryTargetType.Assignment).ActivityId);
    }

    [Fact]
    public async Task NonMember_IsForbidden()
    {
        await Assert.ThrowsAsync<ForbiddenException>(() => Chat(_outsiderUserId).GetMentionTargetsAsync(_programId));
        await Assert.ThrowsAsync<ForbiddenException>(() =>
            Chat(_outsiderUserId).AddMessageAsync(_programId, Post("hi", "c1")));
    }

    // ── Posting ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task Post_WithMentions_StoresOrderedReferences_AndFilterUsesExactTarget()
    {
        var chat = Chat(_managerId);
        var text = $"See {Token(ProgramAdvisoryTargetType.Activity, _activityWithMaterialId)} and " +
                   $"{Token(ProgramAdvisoryTargetType.Module, _moduleId)} and again " +
                   $"{Token(ProgramAdvisoryTargetType.Activity, _activityWithMaterialId)}";

        var message = await chat.AddMessageAsync(_programId, Post(text, "c1"));
        await chat.AddMessageAsync(_programId, Post("no mention", "c2"));

        Assert.Equal(
            [ProgramAdvisoryTargetType.Activity, ProgramAdvisoryTargetType.Module],
            message.References.Select(r => r.TargetType).ToList());
        Assert.All(_db.ProgramAdvisoryReferences.Items, r =>
        {
            Assert.Equal(AdvisoryReferenceContext.WorkingDraft, r.Context);
            Assert.Equal(ProgramAdvisoryAnchorKind.Node, r.AnchorKind);
        });

        var byActivity = await chat.GetMessagesAsync(
            _programId, null, null, 30, ProgramAdvisoryTargetType.Activity, _activityWithMaterialId);
        Assert.Equal([message.Id], byActivity.Messages.Select(m => m.Id).ToList());

        var byCourse = await chat.GetMessagesAsync(
            _programId, null, null, 30, ProgramAdvisoryTargetType.Course, _courseId);
        Assert.Empty(byCourse.Messages);

        var all = await chat.GetMessagesAsync(_programId, null, null, 30);
        Assert.Equal(2, all.Messages.Count);

        await Assert.ThrowsAsync<BadRequestException>(() =>
            chat.GetMessagesAsync(_programId, null, null, 30, ProgramAdvisoryTargetType.Course, null));
    }

    [Fact]
    public async Task Post_UnknownTokenType_IsPlainText()
    {
        var message = await Chat(_managerId).AddMessageAsync(
            _programId, Post($"@[Widget:{Guid.NewGuid()}] hello", "c1"));

        Assert.Empty(message.References);
    }

    [Fact]
    public async Task Post_ForeignMention_IsRejected()
    {
        var ex = await Assert.ThrowsAsync<BadRequestException>(() => Chat(_managerId).AddMessageAsync(
            _programId, Post(Token(ProgramAdvisoryTargetType.Activity, Guid.NewGuid()), "c1")));

        Assert.Equal("MENTION_TARGET_INVALID", ex.ErrorCode);
        Assert.Empty(_db.ProgramAdvisoryDiscussionMessages.Items);
    }

    [Fact]
    public async Task Post_EnforcesLimits()
    {
        var chat = Chat(_managerId);

        var tooLong = await Assert.ThrowsAsync<BadRequestException>(() =>
            chat.AddMessageAsync(_programId, Post(new string('a', 4001), "c1")));
        Assert.Equal("MESSAGE_TOO_LONG", tooLong.ErrorCode);

        var empty = await Assert.ThrowsAsync<BadRequestException>(() =>
            chat.AddMessageAsync(_programId, Post("   ", "c2")));
        Assert.Equal("MESSAGE_EMPTY", empty.ErrorCode);

        var manyMentions = string.Join(' ', Enumerable.Range(0, 21)
            .Select(_ => Token(ProgramAdvisoryTargetType.Activity, Guid.NewGuid())));
        var tooManyMentions = await Assert.ThrowsAsync<BadRequestException>(() =>
            chat.AddMessageAsync(_programId, Post(manyMentions, "c3")));
        Assert.Equal("TOO_MANY_MENTIONS", tooManyMentions.ErrorCode);

        var tooManyAttachments = await Assert.ThrowsAsync<BadRequestException>(() =>
            chat.AddMessageAsync(_programId, Post("x", "c4", Enumerable.Range(0, 11).Select(_ => Guid.NewGuid()).ToList())));
        Assert.Equal("TOO_MANY_ATTACHMENTS", tooManyAttachments.ErrorCode);
    }

    [Fact]
    public async Task Post_LinksOwnUnsentAttachments_AndRejectsOthers()
    {
        var own = SeedAttachment(_managerId);
        var foreign = SeedAttachment(_advisorUserId);
        var chat = Chat(_managerId);

        var rejected = await Assert.ThrowsAsync<BadRequestException>(() =>
            chat.AddMessageAsync(_programId, Post(null, "c1", [foreign.Id])));
        Assert.Equal("ATTACHMENT_INVALID", rejected.ErrorCode);

        var message = await chat.AddMessageAsync(_programId, Post(null, "c2", [own.Id]));

        Assert.Equal(message.Id, own.MessageId);
        Assert.Equal([own.Id], message.Attachments.Select(a => a.Id).ToList());
        Assert.Null(foreign.MessageId);

        var reuse = await Assert.ThrowsAsync<BadRequestException>(() =>
            chat.AddMessageAsync(_programId, Post("again", "c3", [own.Id])));
        Assert.Equal("ATTACHMENT_INVALID", reuse.ErrorCode);
    }

    [Fact]
    public async Task Post_IsIdempotentPerAuthorAndClientMessageId_AndQueuesNotification()
    {
        var chat = Chat(_managerId);

        var first = await chat.AddMessageAsync(_programId, Post("hello", "same"));
        var retry = await chat.AddMessageAsync(_programId, Post("hello", "same"));
        var other = await Chat(_advisorUserId).AddMessageAsync(_programId, Post("hello", "same"));

        Assert.Equal(first.Id, retry.Id);
        Assert.NotEqual(first.Id, other.Id);
        Assert.Equal([1L, 2L], _db.ProgramAdvisoryDiscussionMessages.Items.Select(m => m.Sequence).Order().ToList());
        Assert.Equal(2, _db.ProgramAdvisoryNotificationIntents.Items.Count);
        Assert.All(_db.ProgramAdvisoryNotificationIntents.Items,
            i => Assert.Equal(NotificationType.AdvisoryReply, i.NotificationType));
    }

    // ── Edit / remove ─────────────────────────────────────────────────────────

    [Fact]
    public async Task Edit_ReparsesMentions_AndIsAuthorOnly()
    {
        var chat = Chat(_managerId);
        var message = await chat.AddMessageAsync(
            _programId, Post(Token(ProgramAdvisoryTargetType.Module, _moduleId), "c1"));

        await Assert.ThrowsAsync<ForbiddenException>(() => Chat(_advisorUserId).EditMessageAsync(
            _programId, message.Id, new EditAdvisoryDiscussionMessageRequest { Text = "hijack" }));

        var edited = await Chat(_managerId).EditMessageAsync(
            _programId,
            message.Id,
            new EditAdvisoryDiscussionMessageRequest { Text = $"now {Token(ProgramAdvisoryTargetType.Course, _courseId)}" });

        Assert.NotNull(edited.EditedAt);
        Assert.Equal([ProgramAdvisoryTargetType.Course], edited.References.Select(r => r.TargetType).ToList());
        var counts = await Chat(_managerId).GetMentionCountsAsync(_programId);
        Assert.Equal([(ProgramAdvisoryTargetType.Course, _courseId)], counts.Select(c => (c.TargetType, c.TargetId)).ToList());
    }

    [Fact]
    public async Task Remove_LeavesTombstone_AndDropsMentionsPinAndAttachments()
    {
        var attachment = SeedAttachment(_managerId);
        var message = await Chat(_managerId).AddMessageAsync(
            _programId, Post(Token(ProgramAdvisoryTargetType.Module, _moduleId), "c1", [attachment.Id]));
        await Chat(_advisorUserId).PinMessageAsync(_programId, message.Id);

        await Assert.ThrowsAsync<ForbiddenException>(() => Chat(_advisorUserId).RemoveMessageAsync(_programId, message.Id));
        await Chat(_managerId).RemoveMessageAsync(_programId, message.Id);
        await Chat(_managerId).RemoveMessageAsync(_programId, message.Id);

        var tombstone = await Chat(_managerId).GetMessageAsync(_programId, message.Id);
        Assert.True(tombstone.IsDeleted);
        Assert.Equal(string.Empty, tombstone.Text);
        Assert.Empty(tombstone.References);
        Assert.Empty(tombstone.Attachments);
        Assert.Null(tombstone.Pin);
        Assert.Empty(await Chat(_managerId).GetMentionCountsAsync(_programId));
        Assert.Empty(await Chat(_managerId).GetPinsAsync(_programId));
        await Assert.ThrowsAsync<NotFoundException>(() => Attachments(_managerId).GetUrlAsync(_programId, attachment.Id));
        await Assert.ThrowsAsync<BadRequestException>(() => Chat(_managerId).EditMessageAsync(
            _programId, message.Id, new EditAdvisoryDiscussionMessageRequest { Text = "back" }));
    }

    // ── Pins ──────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Pin_Lifecycle_EnforcesRolesAndTransitions()
    {
        var message = await Chat(_managerId).AddMessageAsync(_programId, Post("Please add a rubric", "c1"));

        await Assert.ThrowsAsync<ForbiddenException>(() => Chat(_managerId).PinMessageAsync(_programId, message.Id));
        var notPinned = await Assert.ThrowsAsync<ConflictException>(() => Chat(_advisorUserId).PerformPinActionAsync(
            _programId, message.Id, new AdvisoryDiscussionPinActionRequest { Action = DiscussionPinAction.Resolve }));
        Assert.Equal("INVALID_STATUS", notPinned.ErrorCode);

        var pinned = await Chat(_advisorUserId).PinMessageAsync(_programId, message.Id);
        Assert.Equal(DiscussionPinStatus.Open, pinned.Pin!.Status);
        Assert.Equal(_advisorUserId, pinned.Pin.PinnedByUserId);
        var repinned = await Chat(_boardUserId).PinMessageAsync(_programId, message.Id);
        Assert.Equal(_advisorUserId, repinned.Pin!.PinnedByUserId);

        await Assert.ThrowsAsync<ForbiddenException>(() => Chat(_advisorUserId).PerformPinActionAsync(
            _programId, message.Id, new AdvisoryDiscussionPinActionRequest { Action = DiscussionPinAction.MarkAddressed }));
        var addressed = await Chat(_managerId).PerformPinActionAsync(
            _programId, message.Id, new AdvisoryDiscussionPinActionRequest { Action = DiscussionPinAction.MarkAddressed });
        Assert.Equal(DiscussionPinStatus.Addressed, addressed.Pin!.Status);
        Assert.Equal(_managerId, addressed.Pin.AddressedByUserId);

        await Assert.ThrowsAsync<ForbiddenException>(() => Chat(_managerId).PerformPinActionAsync(
            _programId, message.Id, new AdvisoryDiscussionPinActionRequest { Action = DiscussionPinAction.Resolve }));
        var resolved = await Chat(_boardUserId).PerformPinActionAsync(
            _programId, message.Id, new AdvisoryDiscussionPinActionRequest { Action = DiscussionPinAction.Resolve });
        Assert.Equal(DiscussionPinStatus.Resolved, resolved.Pin!.Status);

        var again = await Assert.ThrowsAsync<ConflictException>(() => Chat(_boardUserId).PerformPinActionAsync(
            _programId, message.Id, new AdvisoryDiscussionPinActionRequest { Action = DiscussionPinAction.Resolve }));
        Assert.Equal("INVALID_STATUS", again.ErrorCode);
        var addressResolved = await Assert.ThrowsAsync<ConflictException>(() => Chat(_managerId).PerformPinActionAsync(
            _programId, message.Id, new AdvisoryDiscussionPinActionRequest { Action = DiscussionPinAction.MarkAddressed }));
        Assert.Equal("INVALID_STATUS", addressResolved.ErrorCode);

        var reopened = await Chat(_advisorUserId).PerformPinActionAsync(
            _programId, message.Id, new AdvisoryDiscussionPinActionRequest { Action = DiscussionPinAction.Reopen });
        Assert.Equal(DiscussionPinStatus.Open, reopened.Pin!.Status);
        Assert.Null(reopened.Pin.AddressedAt);
        Assert.Null(reopened.Pin.ResolvedAt);

        var unpinned = await Chat(_boardUserId).UnpinMessageAsync(_programId, message.Id);
        Assert.Null(unpinned.Pin);
    }

    [Fact]
    public async Task PinsList_FiltersByStatus_AndMentionCountsCountOpenPins()
    {
        var chat = Chat(_managerId);
        var moduleToken = Token(ProgramAdvisoryTargetType.Module, _moduleId);
        var first = await chat.AddMessageAsync(_programId, Post($"{moduleToken} one", "c1"));
        var second = await chat.AddMessageAsync(_programId, Post($"{moduleToken} two", "c2"));
        await chat.AddMessageAsync(_programId, Post($"{moduleToken} three", "c3"));
        await Chat(_advisorUserId).PinMessageAsync(_programId, first.Id);
        await Chat(_advisorUserId).PinMessageAsync(_programId, second.Id);
        await Chat(_managerId).PerformPinActionAsync(
            _programId, second.Id, new AdvisoryDiscussionPinActionRequest { Action = DiscussionPinAction.MarkAddressed });

        var allPins = await Chat(_managerId).GetPinsAsync(_programId);
        var openPins = await Chat(_managerId).GetPinsAsync(_programId, DiscussionPinStatus.Open);
        var counts = await Chat(_managerId).GetMentionCountsAsync(_programId);

        Assert.Equal([first.Id, second.Id], allPins.Select(p => p.Id).ToList());
        Assert.Equal([first.Id], openPins.Select(p => p.Id).ToList());
        var moduleCount = Assert.Single(counts);
        Assert.Equal(3, moduleCount.MessageCount);
        Assert.Equal(1, moduleCount.OpenPinCount);
    }

    // ── Attachments ───────────────────────────────────────────────────────────

    [Fact]
    public async Task Upload_ValidatesTypeAndSize_AndStoresUnderProgramKey()
    {
        var service = Attachments(_advisorUserId);

        var badType = await Assert.ThrowsAsync<BadRequestException>(() =>
            service.UploadAsync(_programId, File("tool.exe", 10, "application/octet-stream").Object));
        Assert.Equal("ATTACHMENT_TYPE_NOT_ALLOWED", badType.ErrorCode);

        var mismatch = await Assert.ThrowsAsync<BadRequestException>(() =>
            service.UploadAsync(_programId, File("photo.png", 10, "application/pdf").Object));
        Assert.Equal("ATTACHMENT_TYPE_NOT_ALLOWED", mismatch.ErrorCode);

        var tooLarge = await Assert.ThrowsAsync<BadRequestException>(() =>
            service.UploadAsync(_programId, File("big.pdf", 20L * 1024 * 1024 + 1, "application/pdf").Object));
        Assert.Equal("ATTACHMENT_TOO_LARGE", tooLarge.ErrorCode);

        var uploaded = await service.UploadAsync(_programId, File("my photo.png", 10, "image/png").Object);

        Assert.Equal(DiscussionAttachmentKind.Image, uploaded.Kind);
        Assert.Equal("image/png", uploaded.ContentType);
        var stored = Assert.Single(_db.ProgramAdvisoryDiscussionAttachments.Items);
        Assert.Null(stored.MessageId);
        Assert.Equal($"advisory/{_programId:D}/{stored.Id:D}/my_photo.png", stored.StorageKey);
    }

    [Fact]
    public async Task AttachmentUrl_UnsentIsPrivateToUploader_SentIsVisibleToParticipants()
    {
        var attachment = SeedAttachment(_managerId);

        await Assert.ThrowsAsync<NotFoundException>(() => Attachments(_advisorUserId).GetUrlAsync(_programId, attachment.Id));
        var ownUrl = await Attachments(_managerId).GetUrlAsync(_programId, attachment.Id);
        Assert.Equal($"https://signed.test/{attachment.StorageKey}", ownUrl.Url);
        Assert.Equal(_now.AddMinutes(15), ownUrl.ExpiresAt);

        await Chat(_managerId).AddMessageAsync(_programId, Post(null, "c1", [attachment.Id]));

        var shared = await Attachments(_boardUserId).GetUrlAsync(_programId, attachment.Id);
        Assert.Equal(ownUrl.Url, shared.Url);
        _blobService.Verify(b => b.GetFileUrlAsync(attachment.StorageKey, TimeSpan.FromMinutes(15), It.IsAny<CancellationToken>()));
    }

    [Fact]
    public async Task Purge_RemovesOnlyStaleUnsentAttachments()
    {
        var stale = SeedAttachment(_managerId, createdAt: _now.AddHours(-25));
        var recent = SeedAttachment(_managerId, createdAt: _now.AddHours(-1));
        var sentOld = SeedAttachment(_managerId, messageId: Guid.NewGuid(), createdAt: _now.AddDays(-3));

        var purged = await Attachments(_managerId).PurgeUnsentAsync();

        Assert.Equal(1, purged);
        Assert.Equal(
            new HashSet<Guid> { recent.Id, sentOld.Id },
            _db.ProgramAdvisoryDiscussionAttachments.Items.Select(a => a.Id).ToHashSet());
        _blobService.Verify(b => b.DeleteByKeyAsync(stale.StorageKey, It.IsAny<CancellationToken>()), Times.Once);
    }

    // ── Save attachment as material ───────────────────────────────────────────

    [Fact]
    public async Task MaterialFromAttachment_CopiesSentAttachmentToEmptySelfPacedActivity()
    {
        var attachment = SeedAttachment(_managerId);
        await Chat(_managerId).AddMessageAsync(_programId, Post(null, "c1", [attachment.Id]));

        var material = await Materials(_managerId).CreateFromDiscussionAttachmentAsync(
            new CreateMaterialFromDiscussionAttachmentRequest
            {
                AttachmentId = attachment.Id,
                ActivityId = _emptyActivityId,
                Title = "  Worksheet  ",
            });

        Assert.Equal("Worksheet", material.Title);
        Assert.Equal(MaterialType.PDF, material.MaterialType);
        var stored = _db.Materials.Items.Single(m => m.ActivityId == _emptyActivityId);
        Assert.Equal(attachment.SizeBytes, stored.FileSizeBytes);
        _blobService.Verify(b => b.CopyObjectAsync(
            attachment.StorageKey,
            It.Is<string>(key => key.EndsWith(".pdf") && key.Contains(_emptyActivityId.ToString())),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task MaterialFromAttachment_RejectsUnsentWrongTypeAndOccupiedActivity()
    {
        var unsent = SeedAttachment(_managerId);
        var unsentEx = await Assert.ThrowsAsync<BadRequestException>(() => Materials(_managerId).CreateFromDiscussionAttachmentAsync(
            new CreateMaterialFromDiscussionAttachmentRequest { AttachmentId = unsent.Id, ActivityId = _emptyActivityId, Title = "x" }));
        Assert.Equal("ATTACHMENT_INVALID", unsentEx.ErrorCode);

        var zip = SeedAttachment(_managerId, fileName: "bundle.zip");
        var pdf = SeedAttachment(_managerId);
        await Chat(_managerId).AddMessageAsync(_programId, Post(null, "c1", [zip.Id, pdf.Id]));

        var typeEx = await Assert.ThrowsAsync<BadRequestException>(() => Materials(_managerId).CreateFromDiscussionAttachmentAsync(
            new CreateMaterialFromDiscussionAttachmentRequest { AttachmentId = zip.Id, ActivityId = _emptyActivityId, Title = "x" }));
        Assert.Equal("ATTACHMENT_TYPE_NOT_ALLOWED", typeEx.ErrorCode);

        var occupied = await Assert.ThrowsAsync<ConflictException>(() => Materials(_managerId).CreateFromDiscussionAttachmentAsync(
            new CreateMaterialFromDiscussionAttachmentRequest { AttachmentId = pdf.Id, ActivityId = _activityWithMaterialId, Title = "x" }));
        Assert.Equal("MATERIAL_ACTIVITY_INVALID", occupied.ErrorCode);

        var foreignActivity = await Assert.ThrowsAsync<ConflictException>(() => Materials(_managerId).CreateFromDiscussionAttachmentAsync(
            new CreateMaterialFromDiscussionAttachmentRequest { AttachmentId = pdf.Id, ActivityId = Guid.NewGuid(), Title = "x" }));
        Assert.Equal("MATERIAL_ACTIVITY_INVALID", foreignActivity.ErrorCode);
        _blobService.Verify(b => b.CopyObjectAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task MaterialRead_ExpertsOnlyForOwnPrograms()
    {
        Assert.NotNull(await Materials(_advisorUserId).GetMaterialByActivityAsync(_activityWithMaterialId));
        Assert.NotNull(await Materials(_boardUserId).GetMaterialByActivityAsync(_activityWithMaterialId));
        Assert.NotNull(await Materials(_managerId).GetMaterialByActivityAsync(_activityWithMaterialId));

        await Assert.ThrowsAsync<ForbiddenException>(
            () => Materials(_outsiderUserId).GetMaterialByActivityAsync(_activityWithMaterialId));
    }
}
