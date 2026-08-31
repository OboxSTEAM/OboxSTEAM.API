using OboxSteam.Application.Services;
using OboxSteam.Domain.Entities;
using OboxSteam.Domain.Enums;

namespace OboxSteam.Test.UnitTests;

public sealed class SeedTaughtModuleSafetyNetTests
{
    [Fact]
    public void AssignmentWindowBlocksSafetyNetHold_WhenNowIsBeforeStart()
    {
        var now = new DateTime(2026, 8, 31, 16, 0, 0, DateTimeKind.Utc);
        var window = new ClassSession { StartTime = now.AddMinutes(30) };

        Assert.True(SeedService.AssignmentWindowBlocksSafetyNetHold(window, now));
        Assert.False(SeedService.AssignmentWindowBlocksSafetyNetHold(window, now.AddHours(1)));
        Assert.False(SeedService.AssignmentWindowBlocksSafetyNetHold(null, now));
    }

    [Fact]
    public void IsTaughtModuleSafetyNetDraft_MatchesInProgressHoldOnly()
    {
        var draft = new Submission
        {
            Status = SubmissionStatus.TurnedIn,
            ContentText = SeedService.TaughtModuleSafetyNetDraftContent,
            IsDeleted = false
        };
        var fixture = new Submission
        {
            Status = SubmissionStatus.TurnedIn,
            ContentText = "Seeded work waiting for a grade.",
            IsDeleted = false
        };
        var pass = new Submission
        {
            Status = SubmissionStatus.Graded,
            ContentText = SeedService.TaughtModuleSafetyNetPassContent,
            IsDeleted = false
        };

        Assert.True(SeedService.IsTaughtModuleSafetyNetDraft(draft));
        Assert.False(SeedService.IsTaughtModuleSafetyNetDraft(fixture));
        Assert.False(SeedService.IsTaughtModuleSafetyNetDraft(pass));
    }
}
