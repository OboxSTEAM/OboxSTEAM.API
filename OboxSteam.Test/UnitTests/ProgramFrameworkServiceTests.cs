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
    private readonly Guid _otherExpertUserId = Guid.Parse("12121212-1212-1212-1212-121212121212");
    private readonly Guid _managerId = Guid.Parse("13131313-1313-1313-1313-131313131313");
    private readonly Guid _expertId = Guid.Parse("66666666-6666-6666-6666-666666666666");
    private readonly Guid _otherExpertId = Guid.Parse("67676767-6767-6767-6767-676767676767");
    private readonly Guid _frameworkId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private readonly Guid _programId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    private readonly InMemoryUnitOfWork _db = new();
    private readonly Mock<IClaimsService> _claimsService = new();

    private ProgramFrameworkService CreateSut(Guid currentUserId)
    {
        _claimsService.Setup(c => c.GetCurrentUserId).Returns(currentUserId);
        return new ProgramFrameworkService(
            _db,
            _claimsService.Object,
            NullLogger<ProgramFrameworkService>.Instance);
    }

    private void SeedUser(Guid id, RoleType role, string code)
    {
        _db.Users.Seed(new User
        {
            Id = id,
            Code = code,
            Email = $"{code.ToLower()}@test.com",
            FullName = code,
            Role = role,
            Status = AccountStatus.Active,
            IsDeleted = false,
        });
    }

    private Expert SeedExpert(Guid expertId, Guid userId, string code = "EXP-001")
    {
        var expert = new Expert
        {
            Id = expertId,
            Code = code,
            FullName = code,
            UserId = userId,
            IsDeleted = false,
        };
        _db.Experts.Seed(expert);
        return expert;
    }

    private ProgramFramework SeedFramework(
        Guid? id = null,
        Guid? expertId = null,
        string name = "Robotics cơ bản",
        bool requireFinal = false)
    {
        var framework = new ProgramFramework
        {
            Id = id ?? _frameworkId,
            ExpertId = expertId ?? _expertId,
            Name = name,
            Description = "Guideline",
            Category = ProgramCategory.Technology,
            MinOfflineSessions = 1,
            RequireFinalAssessment = requireFinal ? true : null,
            IsDeleted = false,
        };
        _db.ProgramFrameworks.Seed(framework);
        return framework;
    }

    [Fact]
    public async Task Create_AsExpert_PersistsFrameworkAndOptionalCriteria()
    {
        SeedUser(_expertUserId, RoleType.Expert, "USR-EXP");
        SeedExpert(_expertId, _expertUserId);
        var sut = CreateSut(_expertUserId);

        var result = await sut.CreateFrameworkAsync(new CreateProgramFrameworkRequest
        {
            Name = "Lập trình C#",
            Category = ProgramCategory.Technology,
            MinLiveSessions = 2,
            RequireFinalAssessment = true,
            Criteria =
            [
                new FrameworkRubricCriterionRequest
                {
                    Name = "Learning outcomes",
                    MaxScore = 10,
                },
            ],
        });

        Assert.Equal("Lập trình C#", result.Name);
        Assert.Equal(_expertId, result.ExpertId);
        Assert.True(result.RequireFinalAssessment);
        Assert.True(result.RequiresExpertReview);
        Assert.Single(result.Criteria);
        Assert.Single(_db.ProgramFrameworks.Items);
    }

    [Fact]
    public async Task Create_WithZeroCriteria_StillRequiresExpertReview()
    {
        SeedUser(_expertUserId, RoleType.Expert, "USR-EXP");
        SeedExpert(_expertId, _expertUserId);
        var sut = CreateSut(_expertUserId);

        var result = await sut.CreateFrameworkAsync(new CreateProgramFrameworkRequest
        {
            Name = "Open family",
            Category = ProgramCategory.Technology,
        });

        Assert.True(result.RequiresExpertReview);
        Assert.Empty(result.Criteria);
    }

    [Fact]
    public async Task Create_AsManager_Forbidden()
    {
        SeedUser(_managerId, RoleType.Manager, "USR-MGR");
        var sut = CreateSut(_managerId);

        await Assert.ThrowsAsync<ForbiddenException>(
            () => sut.CreateFrameworkAsync(new CreateProgramFrameworkRequest
            {
                Name = "Hộ",
                Category = ProgramCategory.Technology,
            }));
    }

    [Fact]
    public async Task Update_OwningExpert_AllowedWhileProgramPendingReview()
    {
        SeedUser(_expertUserId, RoleType.Expert, "USR-EXP");
        SeedExpert(_expertId, _expertUserId);
        SeedFramework();
        _db.Programs.Seed(new Program
        {
            Id = _programId,
            Code = "PRG-001",
            Name = "Pending",
            Category = ProgramCategory.Technology,
            Status = ProgramStatus.PendingReview,
            FrameworkId = _frameworkId,
            IsDeleted = false,
        });
        var sut = CreateSut(_expertUserId);

        var result = await sut.UpdateFrameworkAsync(_frameworkId, new UpdateProgramFrameworkRequest
        {
            MinOfflineSessions = 2,
        });

        Assert.Equal(2, result.MinOfflineSessions);
    }

    [Fact]
    public async Task Update_ManagerOverride_Allowed()
    {
        SeedUser(_managerId, RoleType.Manager, "USR-MGR");
        SeedExpert(_expertId, _expertUserId);
        SeedFramework();
        var sut = CreateSut(_managerId);

        var result = await sut.UpdateFrameworkAsync(_frameworkId, new UpdateProgramFrameworkRequest
        {
            Description = "Manager override guideline",
        });

        Assert.Equal("Manager override guideline", result.Description);
    }

    [Fact]
    public async Task Delete_Manager_Forbidden()
    {
        SeedUser(_managerId, RoleType.Manager, "USR-MGR");
        SeedExpert(_expertId, _expertUserId);
        SeedFramework();
        var sut = CreateSut(_managerId);

        await Assert.ThrowsAsync<ForbiddenException>(
            () => sut.DeleteFrameworkAsync(_frameworkId));
    }

    [Fact]
    public async Task Delete_OwningExpert_UnlinksPrograms()
    {
        SeedUser(_expertUserId, RoleType.Expert, "USR-EXP");
        SeedExpert(_expertId, _expertUserId);
        SeedFramework();
        _db.Programs.Seed(new Program
        {
            Id = _programId,
            Code = "PRG-001",
            Name = "Linked",
            Category = ProgramCategory.Technology,
            FrameworkId = _frameworkId,
            IsDeleted = false,
        });
        var sut = CreateSut(_expertUserId);

        await sut.DeleteFrameworkAsync(_frameworkId);

        Assert.True(_db.ProgramFrameworks.Items.Single().IsDeleted);
        Assert.Null(_db.Programs.Items.Single().FrameworkId);
    }

    [Fact]
    public async Task GetById_OtherExpert_NotFound()
    {
        SeedUser(_otherExpertUserId, RoleType.Expert, "USR-EXP2");
        SeedExpert(_expertId, _expertUserId);
        SeedExpert(_otherExpertId, _otherExpertUserId, "EXP-002");
        SeedFramework();
        var sut = CreateSut(_otherExpertUserId);

        await Assert.ThrowsAsync<NotFoundException>(
            () => sut.GetFrameworkByIdAsync(_frameworkId));
    }

    [Fact]
    public async Task List_ExpertSeesOnlyOwn_ManagerSeesAll()
    {
        SeedUser(_expertUserId, RoleType.Expert, "USR-EXP");
        SeedUser(_managerId, RoleType.Manager, "USR-MGR");
        SeedExpert(_expertId, _expertUserId);
        SeedExpert(_otherExpertId, _otherExpertUserId, "EXP-002");
        SeedFramework();
        SeedFramework(Guid.Parse("abababab-abab-abab-abab-abababababab"), _otherExpertId, "Other");

        var expertList = await CreateSut(_expertUserId).GetFrameworksAsync(null, null, 1, 10);
        Assert.Single(expertList.Items);

        var managerList = await CreateSut(_managerId).GetFrameworksAsync(null, ProgramCategory.Technology, 1, 10);
        Assert.Equal(2, managerList.Items.Count);
    }

    [Fact]
    public async Task Criteria_ExpertCanAddAndManagerCanOverride()
    {
        SeedUser(_expertUserId, RoleType.Expert, "USR-EXP");
        SeedUser(_managerId, RoleType.Manager, "USR-MGR");
        SeedExpert(_expertId, _expertUserId);
        SeedFramework();

        var created = await CreateSut(_expertUserId).AddCriterionAsync(
            _frameworkId,
            new FrameworkRubricCriterionRequest { Name = "Alignment", MaxScore = 5 });
        Assert.Equal("Alignment", created.Name);

        var updated = await CreateSut(_managerId).UpdateCriterionAsync(
            _frameworkId,
            created.Id,
            new FrameworkRubricCriterionRequest { Name = "Alignment (override)", MaxScore = 8 });
        Assert.Equal(8, updated.MaxScore);
        Assert.Equal("Alignment (override)", updated.Name);
    }

    [Fact]
    public async Task Create_RejectsNonPositiveMinModules()
    {
        SeedUser(_expertUserId, RoleType.Expert, "USR-EXP");
        SeedExpert(_expertId, _expertUserId);
        var sut = CreateSut(_expertUserId);

        await Assert.ThrowsAsync<BadRequestException>(
            () => sut.CreateFrameworkAsync(new CreateProgramFrameworkRequest
            {
                Name = "Bad",
                Category = ProgramCategory.Technology,
                MinModules = 0,
            }));
    }
}
