using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OboxSteam.Application.DTOs.ProgramFrameworkDTO;
using OboxSteam.Application.Exceptions;
using OboxSteam.Application.Interfaces;
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

    private ProgramFrameworkService CreateSut(Guid userId)
    {
        _claims.Setup(c => c.GetCurrentUserId).Returns(userId);
        return new ProgramFrameworkService(_db, _claims.Object, NullLogger<ProgramFrameworkService>.Instance);
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
    public async Task Create_CreatesEditableDraftAndCompleteRubric()
    {
        SeedExpert();
        var result = await CreateSut(_expertUserId).CreateFrameworkAsync(new CreateProgramFrameworkRequest
        {
            Name = "Robotics", Category = ProgramCategory.Technology, MinModules = 2,
            Criteria = [new FrameworkRubricCriterionRequest
            {
                Name = "Alignment", Description = "Outcome alignment", EvidenceGuidance = "Mapped evidence",
                MaxScore = 10,
            }],
        });

        Assert.True(result.HasDraftVersion);
        Assert.Equal(1, result.CurrentVersionNumber);
        Assert.Single(result.Criteria);
        Assert.Single(_db.ProgramFrameworkVersions.Items);
        Assert.Equal(result.CurrentVersionId, _db.FrameworkRubricCriteria.Items.Single().FrameworkVersionId);
    }

    [Fact]
    public async Task PublishedVersion_IsImmutable_AndNewDraftCopiesIt()
    {
        SeedExpert();
        var service = CreateSut(_expertUserId);
        var framework = await service.CreateFrameworkAsync(new CreateProgramFrameworkRequest
        {
            Name = "Robotics", Category = ProgramCategory.Technology,
            Criteria = [new FrameworkRubricCriterionRequest { Name = "Quality", MaxScore = 5 }],
        });
        var published = await service.PublishDraftVersionAsync(framework.Id, framework.CurrentVersionId!.Value);
        await Assert.ThrowsAsync<ConflictException>(() => service.SaveDraftRubricAsync(
            framework.Id, published.Id, new SaveFrameworkRubricRequest()));

        var draft = await service.CreateDraftVersionAsync(framework.Id);
        Assert.Equal(2, draft.VersionNumber);
        Assert.False(draft.IsPublished);
        Assert.Single(draft.Criteria);
        Assert.Equal(published.Id, framework.CurrentVersionId);
    }

    [Fact]
    public async Task FullRubricSave_ReplacesDraftRowsInOneSaveBoundary()
    {
        SeedExpert();
        var service = CreateSut(_expertUserId);
        var framework = await service.CreateFrameworkAsync(new CreateProgramFrameworkRequest
        {
            Name = "Robotics", Category = ProgramCategory.Technology,
            Criteria = [new FrameworkRubricCriterionRequest { Name = "Old", MaxScore = 5 }],
        });
        var result = await service.SaveDraftRubricAsync(
            framework.Id, framework.CurrentVersionId!.Value, new SaveFrameworkRubricRequest
            {
                Criteria =
                [
                    new FrameworkRubricCriterionRequest { Name = "A", MaxScore = 3 },
                    new FrameworkRubricCriterionRequest { Name = "B", MaxScore = 7 },
                ],
            });
        Assert.Equal(2, result.Criteria.Count);
        Assert.Single(_db.FrameworkRubricCriteria.Items, c => c.IsDeleted);
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
    public async Task Create_RejectsZeroConfiguredMinimum()
    {
        SeedExpert();
        await Assert.ThrowsAsync<BadRequestException>(() => CreateSut(_expertUserId).CreateFrameworkAsync(
            new CreateProgramFrameworkRequest
            {
                Name = "Bad", Category = ProgramCategory.Technology, MinModules = 0,
            }));
    }
}
