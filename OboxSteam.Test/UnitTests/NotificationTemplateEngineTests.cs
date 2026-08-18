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

        Assert.Equal("You completed \"Robotics 1\".", command.Body);
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
            moduleName: "Robotics 1"));

        var studentRow = db.Notifications.Items.Single(n => n.RecipientUserId == _studentAId);
        var parentRow = db.Notifications.Items.Single(n => n.RecipientUserId == _parentId);

        Assert.Equal("You completed \"Robotics 1\".", studentRow.Body);
        Assert.Equal("An Nguyen completed \"Robotics 1\".", parentRow.Body);
        Assert.Contains(_studentAId.ToString(), parentRow.PayloadJson, StringComparison.OrdinalIgnoreCase);
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
            "Build a robot"));

        var parentRows = db.Notifications.Items
            .Where(n => n.RecipientUserId == _parentId)
            .ToList();

        Assert.Equal(2, parentRows.Count);
        Assert.Contains(parentRows, n => n.Body == "Assignment \"Build a robot\" is now available for An Nguyen.");
        Assert.Contains(parentRows, n => n.Body == "Assignment \"Build a robot\" is now available for Binh Tran.");
        Assert.Equal(2, db.Notifications.Items.Count(n => n.RecipientUserId == _studentAId || n.RecipientUserId == _studentBId));
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
