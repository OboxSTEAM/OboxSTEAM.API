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

    [Fact]
    public void CreateSeededAssessmentHold_SetsResearchMilestoneId_WhenProvided()
    {
        var assignment = new Assignment
        {
            Id = Guid.NewGuid(),
            PassScore = 60m,
            MaxPoints = 100,
        };
        var milestoneId = Guid.NewGuid();
        var now = DateTime.UtcNow;

        var created = SeedService.CreateSeededAssessmentHold(
            assignment,
            Guid.NewGuid(),
            Guid.NewGuid(),
            moduleFullyTaught: true,
            now,
            milestoneId);

        Assert.Equal(milestoneId, created.ResearchMilestoneId);
        Assert.Equal(SubmissionStatus.Graded, created.Status);
    }

    [Fact]
    public void ApplySeededAssessmentHold_BackfillsResearchMilestoneId_WhenMissing()
    {
        var assignment = new Assignment
        {
            Id = Guid.NewGuid(),
            PassScore = 60m,
            MaxPoints = 100,
        };
        var milestoneId = Guid.NewGuid();
        var existingMilestoneId = Guid.NewGuid();
        var submission = new Submission
        {
            ResearchMilestoneId = null,
            Status = SubmissionStatus.Pending,
        };
        var linked = new Submission
        {
            ResearchMilestoneId = existingMilestoneId,
            Status = SubmissionStatus.ReturnedForRevision,
        };

        SeedService.ApplySeededAssessmentHold(
            submission,
            assignment,
            moduleFullyTaught: true,
            DateTime.UtcNow,
            milestoneId);
        SeedService.ApplySeededAssessmentHold(
            linked,
            assignment,
            moduleFullyTaught: true,
            DateTime.UtcNow,
            milestoneId);

        Assert.Equal(milestoneId, submission.ResearchMilestoneId);
        Assert.Equal(existingMilestoneId, linked.ResearchMilestoneId);
    }

    [Fact]
    public void CreateSeededAssessmentHold_IgnoresEmptyResearchMilestoneId()
    {
        var assignment = new Assignment
        {
            Id = Guid.NewGuid(),
            PassScore = 60m,
            MaxPoints = 100,
        };

        var created = SeedService.CreateSeededAssessmentHold(
            assignment,
            Guid.NewGuid(),
            Guid.NewGuid(),
            moduleFullyTaught: false,
            DateTime.UtcNow,
            Guid.Empty);

        Assert.Null(created.ResearchMilestoneId);
    }

    [Fact]
    public void ApplySeededAssessmentHold_IgnoresEmptyResearchMilestoneId()
    {
        var assignment = new Assignment
        {
            Id = Guid.NewGuid(),
            PassScore = 60m,
            MaxPoints = 100,
        };
        var submission = new Submission
        {
            ResearchMilestoneId = null,
            Status = SubmissionStatus.Pending,
        };

        SeedService.ApplySeededAssessmentHold(
            submission,
            assignment,
            moduleFullyTaught: true,
            DateTime.UtcNow,
            Guid.Empty);

        Assert.Null(submission.ResearchMilestoneId);
    }
}
