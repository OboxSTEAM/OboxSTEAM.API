using Moq;
using OboxSteam.Application.Commons.CurriculumChanges;
using OboxSteam.Application.DTOs.CurriculumChangeDTO;
using OboxSteam.Application.Exceptions;
using OboxSteam.Application.Interfaces;
using OboxSteam.Application.Services;
using OboxSteam.Domain.Entities;
using OboxSteam.Domain.Enums;
using OboxSteam.Test.Helpers;

namespace OboxSteam.Test.UnitTests;

public sealed class CurriculumChangeServiceTests
{
    private readonly InMemoryUnitOfWork _db = new();
    private readonly Mock<IClaimsService> _claims = new();
    private readonly Mock<ICurrentTime> _time = new();
    private readonly Guid _managerId = Guid.NewGuid();
    private readonly Guid _outsiderUserId = Guid.NewGuid();
    private readonly Program _program;
    private readonly Module _moduleOne;
    private readonly Module _moduleTwo;

    public CurriculumChangeServiceTests()
    {
        _claims.SetupGet(c => c.GetCurrentUserId).Returns(_managerId);
        _time.Setup(t => t.GetCurrentTime()).Returns(new DateTime(2026, 9, 30, 12, 0, 0, DateTimeKind.Utc));
        _db.Users.Seed(
            new User { Id = _managerId, Code = "M", Email = "m@test.local", Role = RoleType.Manager, Status = AccountStatus.Active },
            new User { Id = _outsiderUserId, Code = "E", Email = "e@test.local", Role = RoleType.Expert, Status = AccountStatus.Active });
        _db.Experts.Seed(new Expert { Id = Guid.NewGuid(), UserId = _outsiderUserId });

        _moduleOne = new Module { Id = Guid.NewGuid(), Code = "M1", Name = "Module 1", ModuleOrder = 1 };
        _moduleTwo = new Module { Id = Guid.NewGuid(), Code = "M2", Name = "Module 2", ModuleOrder = 2 };
        _program = new Program
        {
            Id = Guid.NewGuid(), Code = "PRG", Name = "Robotics", CurriculumVersion = 3,
            Modules = [_moduleOne, _moduleTwo],
        };
        _moduleOne.ProgramId = _program.Id;
        _moduleTwo.ProgramId = _program.Id;
        _db.Programs.Seed(_program);
        _db.Modules.Seed(_moduleOne, _moduleTwo);

        _db.CurriculumChanges.Seed(
            ModuleRow(_moduleTwo, 1, "name", "Old two", "Module 2"),
            ModuleRow(_moduleOne, 2, "name", "Old one", "Module 1"),
            ModuleRow(_moduleOne, 3, "isMandatory", true, false));
    }

    private CurriculumChange ModuleRow(
        Module module, long version, string fieldKey, object before, object after, Guid? actorUserId = null)
        => new()
        {
            Id = Guid.NewGuid(),
            ProgramId = _program.Id,
            Version = version,
            ActorUserId = actorUserId ?? _managerId,
            ActorName = "Manager",
            At = new DateTime(2026, 9, 30, 9, 0, 0, DateTimeKind.Utc).AddMinutes(version),
            TargetType = ProgramAdvisoryTargetType.Module,
            TargetId = module.Id,
            ChangeKind = CurriculumChangeKind.Updated,
            FieldsJson = CurriculumChangeJson.SerializeFields(
            [
                new CurriculumFieldChange
                {
                    FieldKey = fieldKey,
                    Before = CurriculumChangeJson.ToNode(before),
                    After = CurriculumChangeJson.ToNode(after),
                },
            ]),
            ParentBefore = _program.Id,
            ParentAfter = _program.Id,
            OrderBefore = module.ModuleOrder,
            OrderAfter = module.ModuleOrder,
            LabelSnapshot = module.Name,
            PathSnapshotJson = CurriculumChangeJson.SerializePath(
                [new CurriculumPathSegment(ProgramAdvisoryTargetType.Program, _program.Id, _program.Name)]),
        };

    private CurriculumChangeService CreateSut() => new(_db, _claims.Object, _time.Object);

    [Fact]
    public async Task GetChanges_DefaultBase_WithoutApproval_StartsAtZeroInTreeOrder()
    {
        var result = await CreateSut().GetChangesAsync(_program.Id, null, null);

        Assert.Equal(0, result.FromVersion);
        Assert.Equal(3, result.ToVersion);
        Assert.Equal([_moduleOne.Id, _moduleTwo.Id], result.Items.Select(i => i.TargetId));
        Assert.Equal(2, result.Summary.Updated);
        var moduleOne = result.Items[0];
        Assert.Equal(["name", "isMandatory"], moduleOne.Fields.Select(f => f.FieldKey));
        Assert.Equal("Name", moduleOne.Fields[0].Label);
        Assert.Equal(CurriculumFieldValueType.Boolean, moduleOne.Fields[1].ValueType);
        Assert.Null(moduleOne.Moved);
    }

    [Fact]
    public async Task GetChanges_LastApproval_UsesLatestApprovalEvenWhenRevoked()
    {
        _db.ProgramApprovals.Seed(new ProgramApproval
        {
            Id = Guid.NewGuid(), ProgramId = _program.Id, CurriculumVersion = 2,
            ApprovedAt = new DateTime(2026, 9, 30, 9, 30, 0, DateTimeKind.Utc),
            RevokedAt = new DateTime(2026, 9, 30, 9, 40, 0, DateTimeKind.Utc),
        });

        var result = await CreateSut().GetChangesAsync(_program.Id, "lastApproval", null);

        Assert.Equal(2, result.FromVersion);
        var item = Assert.Single(result.Items);
        Assert.Equal("isMandatory", Assert.Single(item.Fields).FieldKey);
    }

    [Fact]
    public async Task GetChanges_VersionBase_OwnEditsAreNotUnseen()
    {
        _db.CurriculumChangeSeens.Seed(new CurriculumChangeSeen
        {
            Id = Guid.NewGuid(), ProgramId = _program.Id, UserId = _managerId, SeenVersion = 2,
        });

        var result = await CreateSut().GetChangesAsync(_program.Id, "version:1", null);

        Assert.Equal(1, result.FromVersion);
        Assert.Equal(2, result.SeenVersion);
        var item = Assert.Single(result.Items);
        Assert.Equal(_moduleOne.Id, item.TargetId);
        Assert.False(item.IsUnseen);
    }

    [Fact]
    public async Task GetChanges_OtherActorEditAfterSeenVersion_IsUnseen()
    {
        var otherManagerId = Guid.NewGuid();
        _db.CurriculumChanges.Seed(
            ModuleRow(_moduleOne, 3, "code", "M1", "M1-A", otherManagerId),
            ModuleRow(_moduleTwo, 2, "code", "M2", "M2-A", otherManagerId));
        _db.CurriculumChangeSeens.Seed(new CurriculumChangeSeen
        {
            Id = Guid.NewGuid(), ProgramId = _program.Id, UserId = _managerId, SeenVersion = 2,
        });

        var result = await CreateSut().GetChangesAsync(_program.Id, "start", null);

        var moduleOne = result.Items.Single(i => i.TargetId == _moduleOne.Id);
        var moduleTwo = result.Items.Single(i => i.TargetId == _moduleTwo.Id);
        Assert.True(moduleOne.IsUnseen);
        Assert.False(moduleTwo.IsUnseen);
        Assert.Equal([_managerId, otherManagerId], moduleOne.ChangedBy.Select(a => a.UserId));
    }

    [Theory]
    [InlineData("version:9")]
    [InlineData("yesterday")]
    public async Task GetChanges_InvalidBase_ThrowsBadRequest(string baseSpec)
    {
        await Assert.ThrowsAsync<BadRequestException>(() => CreateSut().GetChangesAsync(_program.Id, baseSpec, null));
    }

    [Fact]
    public async Task GetChanges_NonParticipantExpert_ThrowsForbidden()
    {
        _claims.SetupGet(c => c.GetCurrentUserId).Returns(_outsiderUserId);

        await Assert.ThrowsAsync<ForbiddenException>(() => CreateSut().GetChangesAsync(_program.Id, null, null));
    }

    [Fact]
    public async Task MarkSeen_StoresMaximumVersion()
    {
        var sut = CreateSut();

        await sut.MarkSeenAsync(_program.Id, new MarkCurriculumChangesSeenRequest { Version = 2 });
        await sut.MarkSeenAsync(_program.Id, new MarkCurriculumChangesSeenRequest { Version = 1 });

        var seen = Assert.Single(_db.CurriculumChangeSeens.Items);
        Assert.Equal(2, seen.SeenVersion);
    }

    [Fact]
    public async Task MarkSeen_AboveCurrentVersion_ThrowsBadRequest()
    {
        await Assert.ThrowsAsync<BadRequestException>(() =>
            CreateSut().MarkSeenAsync(_program.Id, new MarkCurriculumChangesSeenRequest { Version = 4 }));
    }
}
