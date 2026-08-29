using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OboxSteam.Application.DTOs.NotificationDTO;
using OboxSteam.Application.Interfaces;
using OboxSteam.Application.Notifications;
using OboxSteam.Application.Services;
using OboxSteam.Domain.Entities;
using OboxSteam.Domain.Enums;
using OboxSteam.Test.Helpers;

namespace OboxSteam.Test.UnitTests;

public sealed class NotificationTemplateEngineTests
{
    private readonly Guid _studentAId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private readonly Guid _studentBId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
    private readonly Guid _parentId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");
    private readonly Guid _mentorId = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd");
    private readonly Guid _classId = Guid.Parse("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee");
    private readonly Guid _programId = Guid.Parse("ffffffff-ffff-ffff-ffff-ffffffffffff");

    [Fact]
    public void Renderer_ReplacesKnownTokens()
    {
        var tokens = NotificationTokenKeys.Create(
            studentName: "An",
            moduleName: "Robotics");

        var result = NotificationTemplateRenderer.Interpolate(
            "{studentName} completed \"{moduleName}\".",
            tokens);

        Assert.Equal("An completed \"Robotics\".", result);
    }

    [Fact]
    public void RoleTemplates_FallBackToDefault_WhenVariantMissing()
    {
        var templates = NotificationRoleTemplates.FromDefault("Title", "Default body");

        Assert.Equal("Default body", templates.Resolve(RoleType.Parent).Body);
        Assert.Equal("Default body", templates.Resolve(RoleType.Admin).Body);
    }

    [Fact]
    public void Catalog_ModuleCompleted_HasParentVariantWithStudentToken()
    {
        var command = NotificationCatalog.ModuleCompleted(
            _studentAId,
            Guid.NewGuid(),
            moduleName: "Robotics 1");

        Assert.Equal("Bạn đã hoàn thành \"Robotics 1\".", command.Body);
        Assert.Contains("{studentName}", command.Templates.Parent!.Body);
        Assert.Equal("Robotics 1", command.Tokens[NotificationTokenKeys.ModuleName]);
    }

    [Fact]
    public void Catalog_ParentPaymentRequested_CarriesContextStudent()
    {
        var command = NotificationCatalog.ParentPaymentRequested(
            _parentId,
            _studentAId,
            Guid.NewGuid());

        Assert.Equal(_parentId, command.Audience.UserId);
        Assert.Equal(_studentAId, command.Audience.StudentId);
        Assert.Contains("{studentName}", command.Templates.Default.Body);
    }

    [Fact]
    public async Task Resolver_StudentAndParents_ReturnsRoleAndContextStudent()
    {
        var db = new InMemoryUnitOfWork();
        SeedUser(db, _studentAId, RoleType.Student, "An");
        SeedUser(db, _parentId, RoleType.Parent, "Parent");
        db.ParentStudents.Seed(new ParentStudent
        {
            Id = Guid.NewGuid(),
            ParentId = _parentId,
            StudentId = _studentAId,
            IsVerified = true,
            IsDeleted = false
        });

        var sut = new NotificationRecipientResolver(db);
        var recipients = await sut.ResolveAsync(NotificationAudience.ForStudentAndParents(_studentAId));

        Assert.Equal(2, recipients.Count);
        Assert.Contains(recipients, r => r.UserId == _studentAId && r.Role == RoleType.Student && r.ContextStudentId == _studentAId);
        Assert.Contains(recipients, r => r.UserId == _parentId && r.Role == RoleType.Parent && r.ContextStudentId == _studentAId);
    }

    [Fact]
    public async Task Resolver_ParentsOfStudent_ExcludesStudent()
    {
        var db = new InMemoryUnitOfWork();
        SeedUser(db, _studentAId, RoleType.Student, "An");
        SeedUser(db, _parentId, RoleType.Parent, "Parent");
        db.ParentStudents.Seed(new ParentStudent
        {
            Id = Guid.NewGuid(),
            ParentId = _parentId,
            StudentId = _studentAId,
            IsVerified = true,
            IsDeleted = false
        });

        var sut = new NotificationRecipientResolver(db);
        var recipients = await sut.ResolveAsync(NotificationAudience.ForParentsOfStudent(_studentAId));

        var recipient = Assert.Single(recipients);
        Assert.Equal(_parentId, recipient.UserId);
        Assert.Equal(RoleType.Parent, recipient.Role);
        Assert.Equal(_studentAId, recipient.ContextStudentId);
    }

    [Fact]
    public async Task Resolver_ClassRosterAndParents_EmitsOneParentRowPerChild()
    {
        var db = SeedClassWithTwoChildrenSameParent();
        var sut = new NotificationRecipientResolver(db);

        var recipients = await sut.ResolveAsync(NotificationAudience.ForClassRosterAndParents(_classId));

        var parentRows = recipients.Where(r => r.UserId == _parentId).ToList();
        Assert.Equal(2, parentRows.Count);
        Assert.Contains(parentRows, r => r.ContextStudentId == _studentAId);
        Assert.Contains(parentRows, r => r.ContextStudentId == _studentBId);
        Assert.All(parentRows, r => Assert.Equal(RoleType.Parent, r.Role));
    }

    [Fact]
    public async Task Resolver_ForUser_LooksUpRoleAndContextStudent()
    {
        var db = new InMemoryUnitOfWork();
        SeedUser(db, _parentId, RoleType.Parent, "Parent");
        var sut = new NotificationRecipientResolver(db);

        var recipients = await sut.ResolveAsync(NotificationAudience.ForUser(_parentId, _studentAId));

        var recipient = Assert.Single(recipients);
        Assert.Equal(_parentId, recipient.UserId);
        Assert.Equal(RoleType.Parent, recipient.Role);
        Assert.Equal(_studentAId, recipient.ContextStudentId);
    }

    [Fact]
    public void Catalog_ActivityCompleted_IncludesDeeplinkFields()
    {
        var activityId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var nextActivityId = Guid.Parse("22222222-2222-2222-2222-222222222222");
        var enrollmentId = Guid.Parse("33333333-3333-3333-3333-333333333333");
        var courseId = Guid.Parse("44444444-4444-4444-4444-444444444444");
        var moduleId = Guid.Parse("55555555-5555-5555-5555-555555555555");

        var command = NotificationCatalog.ActivityCompleted(
            _studentAId,
            activityId,
            moduleId,
            _programId,
            "Lesson 1",
            enrollmentId,
            nextActivityId,
            courseId);

        Assert.NotNull(command.Payload);
        Assert.Equal(activityId, command.Payload!.ActivityId);
        Assert.Equal(nextActivityId, command.Payload.NextActivityId);
        Assert.Equal(enrollmentId, command.Payload.EnrollmentId);
        Assert.Equal(enrollmentId, command.Payload.ProgramEnrollmentId);
        Assert.Equal(courseId, command.Payload.CourseId);
        Assert.Equal(_programId, command.Payload.ProgramId);
        Assert.Equal(_studentAId, command.Payload.StudentId);
    }

    [Fact]
    public void Catalog_ProgramActivated_CopiesDisplayNamesOntoPayload()
    {
        var command = NotificationCatalog.ProgramActivated(
            _studentAId,
            _programId,
            Guid.NewGuid(),
            "STEAM 1",
            studentName: "An Nguyen");

        Assert.Equal("An Nguyen", command.Payload!.StudentName);
        Assert.Equal("STEAM 1", command.Payload.ProgramName);
    }

    [Fact]
    public void Catalog_ClassCreated_CopiesClassAndProgramNamesOntoPayload()
    {
        var command = NotificationCatalog.ClassCreated(_classId, _programId, "Cohort A", "Robotics");

        Assert.Equal("Cohort A", command.Payload!.ClassName);
        Assert.Equal("Robotics", command.Payload.ProgramName);
    }

    [Fact]
    public void Catalog_ClassSessionScheduled_DoesNotSetStudentId_UntilPublish()
    {
        var sessionId = Guid.NewGuid();
        var command = NotificationCatalog.ClassSessionScheduled(
            _classId,
            sessionId,
            _programId,
            "Cohort A");

        Assert.Null(command.Payload!.StudentId);
        Assert.Equal("Cohort A", command.Payload.ClassName);
    }

    [Fact]
    public void Catalog_ProgramActivated_SerializesEnrollmentIdAndNextActivityId()
    {
        var enrollmentId = Guid.Parse("33333333-3333-3333-3333-333333333333");
        var nextActivityId = Guid.Parse("22222222-2222-2222-2222-222222222222");

        var command = NotificationCatalog.ProgramActivated(
            _studentAId,
            _programId,
            enrollmentId,
            "STEAM 1",
            nextActivityId);

        var json = System.Text.Json.JsonSerializer.Serialize(
            command.Payload,
            new System.Text.Json.JsonSerializerOptions
            {
                PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase,
                DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
            });

        Assert.Contains("\"enrollmentId\"", json, StringComparison.Ordinal);
        Assert.Contains("\"nextActivityId\"", json, StringComparison.Ordinal);
        Assert.Contains("\"programEnrollmentId\"", json, StringComparison.Ordinal);
        Assert.Contains(enrollmentId.ToString(), json, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(nextActivityId.ToString(), json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Catalog_AssignmentEditedByMentor_IncludesModuleId()
    {
        var assignmentId = Guid.NewGuid();
        var moduleId = Guid.NewGuid();

        var command = NotificationCatalog.AssignmentEditedByMentor(
            assignmentId,
            _mentorId,
            _programId,
            "Quiz 1",
            moduleId);

        Assert.Equal(moduleId, command.Payload!.ModuleId);
        Assert.Equal(assignmentId, command.Payload.AssignmentId);
        Assert.Equal(_programId, command.Payload.ProgramId);
    }

    [Fact]
    public void Catalog_MediaVideoReady_IncludesClassId()
    {
        var mediaId = Guid.NewGuid();
        var command = NotificationCatalog.MediaVideoReady(_mentorId, mediaId, _classId);

        Assert.Equal(mediaId, command.Payload!.MediaAssetId);
        Assert.Equal(_classId, command.Payload.ClassId);
    }

    [Fact]
    public void Catalog_AttendanceMarked_IncludesProgramAndEnrollment()
    {
        var sessionId = Guid.NewGuid();
        var enrollmentId = Guid.NewGuid();
        var activityId = Guid.NewGuid();

        var command = NotificationCatalog.AttendanceMarked(
            AttendanceStatus.Present,
            _studentAId,
            sessionId,
            _classId,
            _mentorId,
            _programId,
            enrollmentId,
            activityId);

        Assert.Equal(_programId, command.Payload!.ProgramId);
        Assert.Equal(enrollmentId, command.Payload.EnrollmentId);
        Assert.Equal(_classId, command.Payload.ClassId);
    }

    [Fact]
    public void Catalog_ClassSessionStarted_IncludesParentsAndParentCopy()
    {
        var sessionId = Guid.NewGuid();
        var command = NotificationCatalog.ClassSessionStarted(
            _classId,
            sessionId,
            _programId,
            "Cohort A");

        Assert.Equal(NotificationAudienceKind.ClassRosterAndParentsAndMentor, command.Audience.Kind);
        Assert.Contains("{studentName}", command.Templates.Parent!.Body);
        Assert.Equal("Một buổi học đã bắt đầu.", command.Templates.Student!.Body);
    }

    [Fact]
    public void Catalog_AttendanceCheckedIn_IsParentOnlyPresentWithClockToken()
    {
        var sessionId = Guid.NewGuid();
        var command = NotificationCatalog.AttendanceCheckedIn(
            _studentAId,
            sessionId,
            "16:00",
            _classId);

        Assert.Equal(NotificationType.AttendanceMarkedPresent, command.Type);
        Assert.Equal(NotificationAudienceKind.ParentsOfStudent, command.Audience.Kind);
        Assert.Equal(_studentAId, command.Audience.StudentId);
        Assert.Equal("16:00", command.Tokens[NotificationTokenKeys.CheckedInAt]);
        Assert.Equal("16:00", command.Payload!.Extra);
        Assert.Contains("{checkedInAt}", command.Templates.Parent!.Body);
    }

    [Fact]
    public void DtoMapper_DeserializesTypedPayload_FromPayloadJson()
    {
        var enrollmentId = Guid.Parse("33333333-3333-3333-3333-333333333333");
        var nextActivityId = Guid.Parse("22222222-2222-2222-2222-222222222222");
        var payloadJson = NotificationDtoMapper.SerializePayload(
            new NotificationPayload
            {
                ProgramId = _programId,
                NextActivityId = nextActivityId,
                StudentId = _studentAId
            }.SetEnrollment(enrollmentId));

        var dto = NotificationDtoMapper.ToDto(new Notification
        {
            Id = Guid.NewGuid(),
            RecipientUserId = _studentAId,
            Type = NotificationType.ActivityCompleted,
            Title = "Activity completed",
            PayloadJson = payloadJson,
            CreatedAt = DateTime.UtcNow
        });

        Assert.NotNull(dto.Payload);
        Assert.Equal(_programId, dto.Payload!.ProgramId);
        Assert.Equal(enrollmentId, dto.Payload.EnrollmentId);
        Assert.Equal(enrollmentId, dto.Payload.ProgramEnrollmentId);
        Assert.Equal(nextActivityId, dto.Payload.NextActivityId);
        Assert.Equal(payloadJson, dto.PayloadJson);
    }

    [Fact]
    public async Task Publisher_RendersParentCopyWithChildName_AndKeepsStudentYouCopy()
    {
        var db = new InMemoryUnitOfWork();
        SeedUser(db, _studentAId, RoleType.Student, "An Nguyen");
        SeedUser(db, _parentId, RoleType.Parent, "Parent");
        db.ParentStudents.Seed(new ParentStudent
        {
            Id = Guid.NewGuid(),
            ParentId = _parentId,
            StudentId = _studentAId,
            IsVerified = true,
            IsDeleted = false
        });

        var dispatcher = new Mock<INotificationDispatcher>();
        dispatcher
            .Setup(d => d.DispatchManyAsync(It.IsAny<IReadOnlyList<NotificationDto>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var sut = new NotificationPublisher(
            db,
            new NotificationRecipientResolver(db),
            dispatcher.Object,
            NullLogger<NotificationPublisher>.Instance);

        await sut.PublishAsync(NotificationCatalog.ModuleCompleted(
            _studentAId,
            Guid.NewGuid(),
            moduleName: "Robotics 1",
            studentName: "An Nguyen",
            programName: "Robotics 1"));

        var studentRow = db.Notifications.Items.Single(n => n.RecipientUserId == _studentAId);
        var parentRow = db.Notifications.Items.Single(n => n.RecipientUserId == _parentId);

        Assert.Equal("Bạn đã hoàn thành \"Robotics 1\".", studentRow.Body);
        Assert.Equal("Con bạn An Nguyen đã hoàn thành \"Robotics 1\".", parentRow.Body);
        Assert.Contains(_studentAId.ToString(), parentRow.PayloadJson, StringComparison.OrdinalIgnoreCase);

        var parentDto = NotificationDtoMapper.ToDto(parentRow);
        Assert.NotNull(parentDto.Payload);
        Assert.Equal(_studentAId, parentDto.Payload!.StudentId);
        Assert.Equal("An Nguyen", parentDto.Payload.StudentName);
        Assert.Equal("Robotics 1", parentDto.Payload.ProgramName);
    }

    [Fact]
    public async Task Publisher_ParentWithTwoChildren_GetsTwoInboxRows()
    {
        var db = SeedClassWithTwoChildrenSameParent();
        var dispatcher = new Mock<INotificationDispatcher>();
        dispatcher
            .Setup(d => d.DispatchManyAsync(It.IsAny<IReadOnlyList<NotificationDto>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var sut = new NotificationPublisher(
            db,
            new NotificationRecipientResolver(db),
            dispatcher.Object,
            NullLogger<NotificationPublisher>.Instance);

        await sut.PublishAsync(NotificationCatalog.AssignmentPublished(
            _classId,
            Guid.NewGuid(),
            _programId,
            "Build a robot",
            className: "Cohort A"));

        var parentRows = db.Notifications.Items
            .Where(n => n.RecipientUserId == _parentId)
            .ToList();

        Assert.Equal(2, parentRows.Count);
        Assert.Contains(parentRows, n => n.Body == "Bài tập \"Build a robot\" hiện đã sẵn sàng cho con bạn An Nguyen.");
        Assert.Contains(parentRows, n => n.Body == "Bài tập \"Build a robot\" hiện đã sẵn sàng cho con bạn Binh Tran.");
        Assert.Equal(2, db.Notifications.Items.Count(n => n.RecipientUserId == _studentAId || n.RecipientUserId == _studentBId));

        var parentDtos = parentRows.Select(NotificationDtoMapper.ToDto).ToList();
        Assert.Contains(parentDtos, d => d.Payload!.StudentId == _studentAId && d.Payload.StudentName == "An Nguyen");
        Assert.Contains(parentDtos, d => d.Payload!.StudentId == _studentBId && d.Payload.StudentName == "Binh Tran");
        Assert.All(parentDtos, d => Assert.Equal("Cohort A", d.Payload!.ClassName));
    }

    private InMemoryUnitOfWork SeedClassWithTwoChildrenSameParent()
    {
        var db = new InMemoryUnitOfWork();
        SeedUser(db, _studentAId, RoleType.Student, "An Nguyen");
        SeedUser(db, _studentBId, RoleType.Student, "Binh Tran");
        SeedUser(db, _parentId, RoleType.Parent, "Parent");
        SeedUser(db, _mentorId, RoleType.Mentor, "Mentor");

        db.Classes.Seed(new Class
        {
            Id = _classId,
            Code = "CLS-001",
            Name = "Cohort A",
            ProgramId = _programId,
            MentorId = _mentorId,
            Status = ClassStatus.InProgress,
            MaxCapacity = 30,
            StartDate = DateTime.UtcNow.AddDays(-7),
            EndDate = DateTime.UtcNow.AddDays(60),
            IsDeleted = false
        });

        db.ClassEnrollments.Seed(
            new ClassEnrollment
            {
                Id = Guid.NewGuid(),
                ClassId = _classId,
                StudentId = _studentAId,
                ProgramEnrollmentId = Guid.NewGuid(),
                Status = ClassEnrollmentStatus.Active,
                IsDeleted = false
            },
            new ClassEnrollment
            {
                Id = Guid.NewGuid(),
                ClassId = _classId,
                StudentId = _studentBId,
                ProgramEnrollmentId = Guid.NewGuid(),
                Status = ClassEnrollmentStatus.Active,
                IsDeleted = false
            });

        db.ParentStudents.Seed(
            new ParentStudent
            {
                Id = Guid.NewGuid(),
                ParentId = _parentId,
                StudentId = _studentAId,
                IsVerified = true,
                IsDeleted = false
            },
            new ParentStudent
            {
                Id = Guid.NewGuid(),
                ParentId = _parentId,
                StudentId = _studentBId,
                IsVerified = true,
                IsDeleted = false
            });

        return db;
    }

    private static void SeedUser(InMemoryUnitOfWork db, Guid id, RoleType role, string fullName)
    {
        db.Users.Seed(new User
        {
            Id = id,
            Code = role.ToString()[..3].ToUpperInvariant() + "-" + id.ToString("N")[..6],
            Email = $"{id:N}@test.com",
            FullName = fullName,
            Role = role,
            Status = AccountStatus.Active,
            IsDeleted = false
        });
    }
}
