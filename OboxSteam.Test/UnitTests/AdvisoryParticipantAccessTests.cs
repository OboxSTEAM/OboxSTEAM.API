using OboxSteam.Application.Commons;
using OboxSteam.Domain.Entities;
using OboxSteam.Domain.Enums;
using OboxSteam.Test.Helpers;

namespace OboxSteam.Test.UnitTests;

public sealed class AdvisoryParticipantAccessTests
{
    private readonly Guid _programId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private readonly Guid _managerId = Guid.Parse("13131313-1313-1313-1313-131313131313");
    private readonly Guid _advisorUserId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private readonly Guid _boardUserId = Guid.Parse("12121212-1212-1212-1212-121212121212");
    private readonly Guid _outsiderUserId = Guid.Parse("14141414-1414-1414-1414-141414141414");
    private readonly Guid _studentId = Guid.Parse("15151515-1515-1515-1515-151515151515");
    private readonly Guid _advisorExpertId = Guid.Parse("66666666-6666-6666-6666-666666666666");
    private readonly Guid _boardExpertId = Guid.Parse("67676767-6767-6767-6767-676767676767");

    private readonly InMemoryUnitOfWork _db = new();

    public AdvisoryParticipantAccessTests()
    {
        SeedUser(_managerId, RoleType.Manager);
        SeedUser(_advisorUserId, RoleType.Expert);
        SeedUser(_boardUserId, RoleType.Expert);
        SeedUser(_outsiderUserId, RoleType.Expert);
        SeedUser(_studentId, RoleType.Student);
        _db.Experts.Seed(
            new Expert { Id = _advisorExpertId, Code = "EXP-ADV", FullName = "Advisor", UserId = _advisorUserId },
            new Expert { Id = _boardExpertId, Code = "EXP-BRD", FullName = "Board", UserId = _boardUserId },
            new Expert { Id = Guid.NewGuid(), Code = "EXP-OUT", FullName = "Outsider", UserId = _outsiderUserId });
        _db.Programs.Seed(new Program
        {
            Id = _programId,
            Code = "PRG-001",
            Name = "Robotics",
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
    }

    private void SeedUser(Guid id, RoleType role)
        => _db.Users.Seed(new User
        {
            Id = id,
            Code = id.ToString("N")[..8],
            Email = $"{id:N}@test.com",
            FullName = role.ToString(),
            Role = role,
            Status = AccountStatus.Active,
        });

    [Fact]
    public async Task IsParticipant_TrueForManagerAdvisorAndBoardExpert()
    {
        Assert.True(await AdvisoryParticipantAccess.IsParticipantAsync(_db, _managerId, _programId));
        Assert.True(await AdvisoryParticipantAccess.IsParticipantAsync(_db, _advisorUserId, _programId));
        Assert.True(await AdvisoryParticipantAccess.IsParticipantAsync(_db, _boardUserId, _programId));
    }

    [Fact]
    public async Task IsParticipant_FalseForOutsidersUnknownIdsAndDeletedPrograms()
    {
        Assert.False(await AdvisoryParticipantAccess.IsParticipantAsync(_db, _outsiderUserId, _programId));
        Assert.False(await AdvisoryParticipantAccess.IsParticipantAsync(_db, _studentId, _programId));
        Assert.False(await AdvisoryParticipantAccess.IsParticipantAsync(_db, Guid.Empty, _programId));
        Assert.False(await AdvisoryParticipantAccess.IsParticipantAsync(_db, Guid.NewGuid(), _programId));
        Assert.False(await AdvisoryParticipantAccess.IsParticipantAsync(_db, _managerId, Guid.NewGuid()));

        _db.Programs.Items.Single().IsDeleted = true;
        Assert.False(await AdvisoryParticipantAccess.IsParticipantAsync(_db, _managerId, _programId));
    }
}
