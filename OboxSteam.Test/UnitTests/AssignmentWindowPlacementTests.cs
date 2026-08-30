using OboxSteam.Application.Validation;
using OboxSteam.Domain.Entities;

namespace OboxSteam.Test.UnitTests;

public sealed class AssignmentWindowPlacementTests
{
    private readonly DateTime _classStart = new(2026, 9, 5, 0, 0, 0, DateTimeKind.Utc);
    private readonly DateTime _classEnd = new(2026, 11, 14, 0, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void ResolveRelatedTeachingEnd_UsesCourseThenModuleThenClassStart()
    {
        var moduleId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var courseId = Guid.Parse("22222222-2222-2222-2222-222222222222");
        var otherCourseId = Guid.Parse("33333333-3333-3333-3333-333333333333");
        var lives = new List<AssignmentWindowPlacement.ScheduledLive>
        {
            new(Guid.NewGuid(), moduleId, courseId, _classStart.AddHours(9), _classStart.AddHours(11)),
            new(Guid.NewGuid(), moduleId, otherCourseId, _classStart.AddDays(7).AddHours(9), _classStart.AddDays(7).AddHours(11)),
        };

        Assert.Equal(
            _classStart.AddHours(11),
            AssignmentWindowPlacement.ResolveRelatedTeachingEnd(_classStart, lives, moduleId, courseId, null));
        Assert.Equal(
            _classStart.AddDays(7).AddHours(11),
            AssignmentWindowPlacement.ResolveRelatedTeachingEnd(_classStart, lives, moduleId, null, null));
        Assert.Equal(
            _classStart,
            AssignmentWindowPlacement.ResolveRelatedTeachingEnd(_classStart, [], Guid.NewGuid(), null, null));
    }

    [Fact]
    public void ResolveRelatedTeachingEnd_PrefersMilestoneLives()
    {
        var moduleId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var courseId = Guid.Parse("22222222-2222-2222-2222-222222222222");
        var milestoneActivityId = Guid.Parse("44444444-4444-4444-4444-444444444444");
        var laterActivityId = Guid.Parse("55555555-5555-5555-5555-555555555555");
        var lives = new List<AssignmentWindowPlacement.ScheduledLive>
        {
            new(milestoneActivityId, moduleId, courseId, _classStart.AddHours(9), _classStart.AddHours(11)),
            new(laterActivityId, moduleId, courseId, _classStart.AddDays(7).AddHours(9), _classStart.AddDays(7).AddHours(11)),
        };

        Assert.Equal(
            _classStart.AddHours(11),
            AssignmentWindowPlacement.ResolveRelatedTeachingEnd(
                _classStart,
                lives,
                moduleId,
                courseId,
                [milestoneActivityId]));
    }

    [Fact]
    public void TryComputeWindow_BumpsToFortyEightHours_ThenClampsToClassEnd()
    {
        var open = _classStart.AddHours(11);
        var nextLive = open.AddHours(22);
        Assert.True(AssignmentWindowPlacement.TryComputeWindow(
            open,
            nextLive,
            _classEnd,
            out var close,
            out var error));
        Assert.Null(error);
        Assert.Equal(open.AddHours(48), close);

        var shortClassEnd = open.Date.AddDays(1);
        Assert.True(AssignmentWindowPlacement.TryComputeWindow(
            open,
            nextLive,
            shortClassEnd,
            out var clamped,
            out _));
        Assert.Equal(AssignmentWindowPlacement.EndOfClassDay(shortClassEnd), clamped);
    }

    [Fact]
    public void TryComputeWindow_Fails_WhenCloseWouldNotFollowOpen()
    {
        var open = AssignmentWindowPlacement.EndOfClassDay(_classEnd).AddTicks(1);
        Assert.False(AssignmentWindowPlacement.TryComputeWindow(
            open,
            nextLiveStart: null,
            _classEnd,
            out _,
            out var error));
        Assert.Contains("Extend the class end date", error);
    }

    [Fact]
    public void MilestoneLiveActivityIds_ReturnsLinkedLivesOnly()
    {
        var assignmentId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var milestoneId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
        var liveActivityId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");
        var assignment = new Assignment { Id = assignmentId, ModuleId = Guid.NewGuid() };
        var milestones = new List<ResearchMilestone>
        {
            new()
            {
                Id = milestoneId,
                AssignmentId = assignmentId,
                ModuleId = assignment.ModuleId,
                IsDeleted = false,
            },
        };
        var links = new List<ResearchMilestoneActivity>
        {
            new()
            {
                Id = Guid.NewGuid(),
                ResearchMilestoneId = milestoneId,
                ActivityId = liveActivityId,
                IsDeleted = false,
            },
            new()
            {
                Id = Guid.NewGuid(),
                ResearchMilestoneId = milestoneId,
                ActivityId = Guid.NewGuid(),
                IsDeleted = false,
            },
        };
        var lives = new List<AssignmentWindowPlacement.ScheduledLive>
        {
            new(liveActivityId, assignment.ModuleId, Guid.NewGuid(), _classStart, _classStart.AddHours(2)),
        };

        var ids = AssignmentWindowPlacement.MilestoneLiveActivityIds(assignment, milestones, links, lives);
        Assert.Equal(liveActivityId, Assert.Single(ids!));
    }
}
