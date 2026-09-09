using OboxSteam.Application.Commons;
using OboxSteam.Application.Exceptions;
using OboxSteam.Application.Validation;
using OboxSteam.Domain.Entities;
using OboxSteam.Domain.Enums;
using OboxSteam.Test.Helpers;

namespace OboxSteam.Test.UnitTests;

public sealed class ProgramFrameworkValidatorTests
{
    private readonly Guid _programId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private readonly Guid _frameworkId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private readonly Guid _versionId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
    private readonly InMemoryUnitOfWork _db = new();

    [Fact]
    public void CollectRuleFailures_SkipsBlankRules()
    {
        Assert.Empty(ProgramFrameworkValidator.CollectRuleFailures(
            new ProgramFrameworkVersion(), EmptySnapshot()));
    }

    [Fact]
    public void CollectRuleFailures_ReturnsEveryStructuralFailure()
    {
        var version = new ProgramFrameworkVersion
        {
            MinModules = 2, MinOfflineSessions = 1, MinLiveSessions = 1,
            RequireCapstoneResearchMilestone = true,
        };
        var errors = ProgramFrameworkValidator.CollectRuleFailures(version, EmptySnapshot());
        Assert.Equal(4, errors.Count);
        Assert.Contains(errors, e => e.Contains("module", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(errors, e => e.Contains("Offline", StringComparison.Ordinal));
        Assert.Contains(errors, e => e.Contains("LiveOnline", StringComparison.Ordinal));
        Assert.Contains(errors, e => e.Contains("capstone", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateForSubmit_UsesPinnedPublishedVersion()
    {
        _db.Programs.Seed(new Program
        {
            Id = _programId, Code = "PRG", Name = "Program", Category = ProgramCategory.Technology,
            FrameworkId = _frameworkId, FrameworkVersionId = _versionId,
        });
        _db.ProgramFrameworkVersions.Seed(new ProgramFrameworkVersion
        {
            Id = _versionId, FrameworkId = _frameworkId, VersionNumber = 1,
            IsPublished = true, MinModules = 1,
        });

        var error = await Assert.ThrowsAsync<BadRequestException>(
            () => ProgramFrameworkValidator.ValidateForSubmitAsync(_db, _programId));
        Assert.Contains("module", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ValidateForSubmit_RejectsDraftPinnedVersion()
    {
        _db.Programs.Seed(new Program
        {
            Id = _programId, Code = "PRG", Name = "Program", Category = ProgramCategory.Technology,
            FrameworkId = _frameworkId, FrameworkVersionId = _versionId,
        });
        _db.ProgramFrameworkVersions.Seed(new ProgramFrameworkVersion
        {
            Id = _versionId, FrameworkId = _frameworkId, VersionNumber = 1,
        });

        await Assert.ThrowsAsync<ConflictException>(
            () => ProgramFrameworkValidator.ValidateForSubmitAsync(_db, _programId));
    }

    [Fact]
    public void ValidatePositiveConstraint_RejectsZero()
    {
        Assert.Throws<BadRequestException>(
            () => ProgramFrameworkValidator.ValidatePositiveConstraint("MinModules", 0));
    }

    private ProgramCurriculumTreeSnapshot EmptySnapshot() => new()
    {
        Program = new Program
        {
            Id = _programId, Code = "PRG", Name = "Program", Category = ProgramCategory.Technology,
        },
    };
}
