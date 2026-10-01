using OboxSteam.Application.Realtime;

namespace OboxSteam.Test.UnitTests;

public sealed class AdvisoryPresenceTrackerTests
{
    private readonly Guid _programId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private readonly Guid _otherProgramId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private readonly Guid _userId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private readonly AdvisoryPresenceTracker _tracker = new();

    [Fact]
    public void Join_MakesUserPresentOnlyInThatProgram()
    {
        _tracker.Join(_programId, _userId, "c1");

        Assert.True(_tracker.IsPresent(_programId, _userId));
        Assert.False(_tracker.IsPresent(_otherProgramId, _userId));
        Assert.False(_tracker.IsPresent(_programId, Guid.NewGuid()));
    }

    [Fact]
    public void Leave_KeepsUserPresentWhileAnotherConnectionRemains()
    {
        _tracker.Join(_programId, _userId, "c1");
        _tracker.Join(_programId, _userId, "c2");

        _tracker.Leave(_programId, "c1");
        Assert.True(_tracker.IsPresent(_programId, _userId));

        _tracker.Leave(_programId, "c2");
        Assert.False(_tracker.IsPresent(_programId, _userId));
    }

    [Fact]
    public void Disconnect_RemovesEveryProgramOfTheConnection()
    {
        _tracker.Join(_programId, _userId, "c1");
        _tracker.Join(_otherProgramId, _userId, "c1");
        _tracker.Join(_otherProgramId, _userId, "c2");

        _tracker.Disconnect("c1");

        Assert.False(_tracker.IsPresent(_programId, _userId));
        Assert.True(_tracker.IsPresent(_otherProgramId, _userId));
    }

    [Fact]
    public void UnknownConnections_AreIgnored()
    {
        _tracker.Leave(_programId, "missing");
        _tracker.Disconnect("missing");
        _tracker.Join(_programId, _userId, "c1");
        _tracker.Leave(_otherProgramId, "c1");

        Assert.True(_tracker.IsPresent(_programId, _userId));
    }

    [Fact]
    public void RejoinWithAnotherUser_ReplacesThePreviousMembership()
    {
        var otherUserId = Guid.NewGuid();
        _tracker.Join(_programId, _userId, "c1");

        _tracker.Join(_programId, otherUserId, "c1");

        Assert.False(_tracker.IsPresent(_programId, _userId));
        Assert.True(_tracker.IsPresent(_programId, otherUserId));
    }
}
