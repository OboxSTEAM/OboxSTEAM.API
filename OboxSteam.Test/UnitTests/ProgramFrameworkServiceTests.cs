using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OboxSteam.Application.DTOs.ProgramFrameworkDTO;
using OboxSteam.Application.Exceptions;
using OboxSteam.Application.Interfaces;
using OboxSteam.Application.Notifications;
using OboxSteam.Application.Services;
using OboxSteam.Domain.Entities;
using OboxSteam.Domain.Enums;
using OboxSteam.Test.Helpers;

namespace OboxSteam.Test.UnitTests;

public sealed class ProgramFrameworkServiceTests
{
    private readonly Guid _expertUserId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private readonly Guid _managerId = Guid.Parse("13131313-1313-1313-1313-131313131313");
    private readonly Guid _expertId = Guid.Parse("66666666-6666-6666-6666-666666666666");
    private readonly Guid _frameworkId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private readonly InMemoryUnitOfWork _db = new();
    private readonly Mock<IClaimsService> _claims = new();
    private readonly Mock<INotificationPublisher> _notificationPublisher = new();

    private ProgramFrameworkService CreateSut(Guid userId)
    {
        _claims.Setup(c => c.GetCurrentUserId).Returns(userId);
        return new ProgramFrameworkService(
            _db,
            _claims.Object,
            _notificationPublisher.Object,
            NullLogger<ProgramFrameworkService>.Instance);
    }

    private void SeedUser(Guid id, RoleType role, string code)
    {
        _db.Users.Seed(new User
        {
            Id = id, Code = code, Email = $"{code}@test.local", FullName = code,
            Role = role, Status = AccountStatus.Active,
        });
    }

    private void SeedExpert()
    {
        SeedUser(_expertUserId, RoleType.Expert, "EXP-U");
        _db.Experts.Seed(new Expert
        {
            Id = _expertId, Code = "EXP-1", FullName = "Expert", UserId = _expertUserId,
        });
    }

    [Fact]
    public async Task Create_CreatesEditableDraftWithRules()
    {
        SeedExpert();
        var result = await CreateSut(_expertUserId).CreateFrameworkAsync(new CreateProgramFrameworkRequest
        {
            Name = "Robotics", Category = ProgramCategory.Technology, MinModules = 2, MaxModules = 6,
            MinTotalHours = 10, RequireAssignmentPerModule = true, MinOfflineRatioPercent = 30,
        });

        Assert.True(result.HasDraftVersion);
        Assert.Equal(1, result.CurrentVersionNumber);
        Assert.Equal(6, result.MaxModules);
        Assert.True(result.RequireAssignmentPerModule);
        var version = Assert.Single(_db.ProgramFrameworkVersions.Items);
        Assert.Equal(2, version.MinModules);
        Assert.Equal(10, version.MinTotalHours);
        Assert.Equal(30, version.MinOfflineRatioPercent);
    }

    [Fact]
    public async Task PublishedVersion_NewDraftCopiesAllRules()
    {
        SeedExpert();
        var service = CreateSut(_expertUserId);
        var framework = await service.CreateFrameworkAsync(new CreateProgramFrameworkRequest
        {
            Name = "Robotics", Category = ProgramCategory.Technology,
            MaxActivityMinutes = 120, RequireActivityDuration = true, MinMaterialsPerActivity = 1,
            RequireCategoryMatch = true, MinDescriptionLength = 50, MinSkillsGained = 2, RequireThumbnail = true,
        });
        var published = await service.PublishDraftVersionAsync(framework.Id, framework.CurrentVersionId!.Value);

        var draft = await service.CreateDraftVersionAsync(framework.Id);

        Assert.Equal(2, draft.VersionNumber);
        Assert.False(draft.IsPublished);
        Assert.Equal(120, draft.MaxActivityMinutes);
        Assert.True(draft.RequireActivityDuration);
        Assert.Equal(1, draft.MinMaterialsPerActivity);
        Assert.True(draft.RequireCategoryMatch);
        Assert.Equal(50, draft.MinDescriptionLength);
        Assert.Equal(2, draft.MinSkillsGained);
        Assert.True(draft.RequireThumbnail);
        Assert.True(published.IsPublished);
    }

    [Fact]
    public async Task Update_NullLeavesRuleUnchanged_ClearFlagTurnsItOff()
    {
        SeedExpert();
        var service = CreateSut(_expertUserId);
        var framework = await service.CreateFrameworkAsync(new CreateProgramFrameworkRequest
        {
            Name = "Robotics", Category = ProgramCategory.Technology,
            MinModules = 2, MaxModules = 5, RequireThumbnail = true,
        });

        var result = await service.UpdateFrameworkAsync(framework.Id, new UpdateProgramFrameworkRequest
        {
            ClearMaxModules = true,
            MinTotalHours = 8,
            RequireThumbnail = false,
        });

        Assert.Equal(2, result.MinModules);
        Assert.Null(result.MaxModules);
        Assert.Equal(8, result.MinTotalHours);
        Assert.False(result.RequireThumbnail);
    }

    [Fact]
    public async Task Update_MinAboveMax_ReturnsRulesInvalid()
    {
        SeedExpert();
        var service = CreateSut(_expertUserId);
        var framework = await service.CreateFrameworkAsync(new CreateProgramFrameworkRequest
        {
            Name = "Robotics", Category = ProgramCategory.Technology, MaxCoursesPerModule = 3,
        });

        var error = await Assert.ThrowsAsync<BadRequestException>(() => service.UpdateFrameworkAsync(
            framework.Id,
            new UpdateProgramFrameworkRequest { MinCoursesPerModule = 4 }));

        Assert.Equal("FRAMEWORK_RULES_INVALID", error.ErrorCode);
        Assert.Equal(3, _db.ProgramFrameworkVersions.Items.Single().MaxCoursesPerModule);
    }

    [Fact]
    public async Task Archive_PreservesPublishedVersions()
    {
        SeedExpert();
        var service = CreateSut(_expertUserId);
        var framework = await service.CreateFrameworkAsync(new CreateProgramFrameworkRequest
        {
            Name = "Robotics", Category = ProgramCategory.Technology,
        });
        await service.PublishDraftVersionAsync(framework.Id, framework.CurrentVersionId!.Value);
        var archived = await service.ArchiveFrameworkAsync(framework.Id);
        Assert.True(archived.IsArchived);
        Assert.False(_db.ProgramFrameworks.Items.Single().IsDeleted);
        Assert.Single(_db.ProgramFrameworkVersions.Items);
    }

    [Fact]
    public async Task Manager_CannotMutateFramework()
    {
        SeedUser(_managerId, RoleType.Manager, "MGR");
        _db.ProgramFrameworks.Seed(new ProgramFramework
        {
            Id = _frameworkId, ExpertId = _expertId, Name = "Robotics", Category = ProgramCategory.Technology,
        });
        await Assert.ThrowsAsync<ForbiddenException>(() => CreateSut(_managerId).ArchiveFrameworkAsync(_frameworkId));
    }

    [Fact]
    public async Task Publish_NotifiesManagersOfProgramsOnOlderVersions()
    {
        SeedExpert();
        var service = CreateSut(_expertUserId);
        var framework = await service.CreateFrameworkAsync(new CreateProgramFrameworkRequest
        {
            Name = "Robotics", Category = ProgramCategory.Technology,
        });
        var v1 = await service.PublishDraftVersionAsync(framework.Id, framework.CurrentVersionId!.Value);
        var olderProgramId = Guid.NewGuid();
        _db.Programs.Seed(
            new Program
            {
                Id = olderProgramId, Code = "PRG-OLD", Name = "Old", FrameworkId = framework.Id, FrameworkVersionId = v1.Id,
            },
            new Program
            {
                Id = Guid.NewGuid(), Code = "PRG-NOFW", Name = "No framework",
            });
        var draft = await service.CreateDraftVersionAsync(framework.Id);
        IReadOnlyList<NotificationCommand>? sent = null;
        _notificationPublisher
            .Setup(p => p.PublishManyAsync(It.IsAny<IReadOnlyList<NotificationCommand>>(), It.IsAny<CancellationToken>()))
            .Callback<IReadOnlyList<NotificationCommand>, CancellationToken>((commands, _) => sent = commands)
            .Returns(Task.CompletedTask);

        await service.PublishDraftVersionAsync(framework.Id, draft.Id);

        var command = Assert.Single(sent!);
        Assert.Equal(NotificationType.FrameworkVersionPublished, command.Type);
        Assert.Equal(olderProgramId, command.Payload!.ProgramId);
        Assert.Equal(1, command.Payload.FromVersion);
        Assert.Equal(2, command.Payload.ToVersion);
    }

    [Fact]
    public async Task Create_AllowsZeroMinimum()
    {
        SeedExpert();
        var result = await CreateSut(_expertUserId).CreateFrameworkAsync(new CreateProgramFrameworkRequest
        {
            Name = "Zero", Category = ProgramCategory.Technology, MinModules = 0,
        });

        Assert.Equal(0, result.MinModules);
    }

    [Fact]
    public async Task Create_RejectsNegativeValue()
    {
        SeedExpert();
        var error = await Assert.ThrowsAsync<BadRequestException>(() => CreateSut(_expertUserId).CreateFrameworkAsync(
            new CreateProgramFrameworkRequest
            {
                Name = "Bad", Category = ProgramCategory.Technology, MinModules = -1,
            }));

        Assert.Equal("FRAMEWORK_RULES_INVALID", error.ErrorCode);
        Assert.Empty(_db.ProgramFrameworks.Items);
    }
}
