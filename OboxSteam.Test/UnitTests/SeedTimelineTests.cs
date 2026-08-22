using OboxSteam.Application.Services;
using OboxSteam.Domain.Enums;

namespace OboxSteam.Test.UnitTests;

public sealed class SeedTimelineTests
{
    [Fact]
    public void ResolveSessionStatus_ReturnsCompleted_WhenEndIsInThePast()
    {
        var now = new DateTime(2026, 8, 21, 12, 0, 0, DateTimeKind.Utc);
        var start = now.AddHours(-3);
        var end = now.AddHours(-1);

        var status = SeedTimeline.ResolveSessionStatus(start, end, now);

        Assert.Equal(ClassSessionStatus.Completed, status);
    }

    [Fact]
    public void ResolveSessionStatus_ReturnsInProgress_WhenNowIsInsideWindow()
    {
        var now = new DateTime(2026, 8, 21, 10, 0, 0, DateTimeKind.Utc);
        var start = now.AddMinutes(-30);
        var end = now.AddMinutes(90);

        var status = SeedTimeline.ResolveSessionStatus(start, end, now);

        Assert.Equal(ClassSessionStatus.InProgress, status);
    }

    [Fact]
    public void ResolveSessionStatus_ReturnsScheduled_WhenStartIsInTheFuture()
    {
        var now = new DateTime(2026, 8, 21, 12, 0, 0, DateTimeKind.Utc);
        var start = now.AddDays(2);
        var end = start.AddHours(2);

        var status = SeedTimeline.ResolveSessionStatus(start, end, now);

        Assert.Equal(ClassSessionStatus.Scheduled, status);
    }

    [Fact]
    public void TryResolveSlotSequence_LandsOnConfiguredWeekday()
    {
        var classStart = new DateTime(2026, 8, 17, 0, 0, 0, DateTimeKind.Utc); // Monday
        var classEnd = classStart.AddDays(28);
        var slots = new SeedTimeline.WeekdaySlot[]
        {
            new(DayOfWeek.Tuesday, 9, 0, 150),
            new(DayOfWeek.Thursday, 9, 0, 150),
        };

        var first = SeedTimeline.TryResolveSlotSequence(classStart, classEnd, slots, 0);
        var second = SeedTimeline.TryResolveSlotSequence(classStart, classEnd, slots, 1);

        Assert.NotNull(first);
        Assert.Equal(DayOfWeek.Tuesday, first.Value.StartTime.DayOfWeek);
        Assert.Equal(9, first.Value.StartTime.Hour);
        Assert.Equal(150, (first.Value.EndTime - first.Value.StartTime).TotalMinutes);

        Assert.NotNull(second);
        Assert.Equal(DayOfWeek.Thursday, second.Value.StartTime.DayOfWeek);
        Assert.False(SeedTimeline.RangesOverlap(
            first.Value.StartTime,
            first.Value.EndTime,
            second.Value.StartTime,
            second.Value.EndTime));
    }

    [Fact]
    public void TryResolveSlotSequence_ReturnsNull_WhenIndexExceedsWindow()
    {
        var classStart = new DateTime(2026, 8, 17, 0, 0, 0, DateTimeKind.Utc);
        var classEnd = classStart.AddDays(6);
        var slots = new SeedTimeline.WeekdaySlot[]
        {
            new(DayOfWeek.Wednesday, 18, 0, 150),
        };

        var missing = SeedTimeline.TryResolveSlotSequence(classStart, classEnd, slots, 3);

        Assert.Null(missing);
    }

    [Fact]
    public void RangesOverlap_DetectsConflictAndGap()
    {
        var aStart = new DateTime(2026, 8, 21, 9, 0, 0, DateTimeKind.Utc);
        var aEnd = aStart.AddHours(2);
        var overlapStart = aStart.AddHours(1);
        var laterStart = aEnd.AddMinutes(30);

        Assert.True(SeedTimeline.RangesOverlap(aStart, aEnd, overlapStart, overlapStart.AddHours(2)));
        Assert.False(SeedTimeline.RangesOverlap(aStart, aEnd, laterStart, laterStart.AddHours(2)));
    }

    [Fact]
    public void AttendanceForIndex_MixesStatuses()
    {
        Assert.Equal(AttendanceStatus.Late, SeedTimeline.AttendanceForIndex(0, 0));
        Assert.Equal(AttendanceStatus.Absent, SeedTimeline.AttendanceForIndex(1, 0));
        Assert.Equal(AttendanceStatus.Excused, SeedTimeline.AttendanceForIndex(2, 0));
        Assert.Equal(AttendanceStatus.Present, SeedTimeline.AttendanceForIndex(3, 0));
    }

    [Fact]
    public void AtDaysAndAtMonths_OffsetFromCapturedNow()
    {
        var now = new DateTime(2026, 8, 21, 0, 0, 0, DateTimeKind.Utc);
        var clock = new SeedTimeline(now);

        Assert.Equal(now.AddDays(-42), clock.AtDays(-42));
        Assert.Equal(now.AddMonths(-8), clock.AtMonths(-8));
    }

    [Fact]
    public void ResolveSeedVenue_Lesson_HasRoomAndFakeMeetUrl()
    {
        var (location, meetingUrl) = SeedTimeline.ResolveSeedVenue(
            SessionKind.Lesson,
            "CLS-ROBOTICS-CURRENT",
            3);

        Assert.Equal("NVH 603", location);
        Assert.Equal("https://meet.oboxsteam.com/cls-robotics-current/s03", meetingUrl);
    }

    [Fact]
    public void ResolveSeedVenue_FieldTrip_HasLabOnly()
    {
        var (location, meetingUrl) = SeedTimeline.ResolveSeedVenue(
            SessionKind.FieldTrip,
            "CLS-IOT-CURRENT",
            1);

        Assert.Equal("Campus Lab 2", location);
        Assert.Null(meetingUrl);
    }

    [Fact]
    public void ResolveSeedVenue_AssignmentWindow_HasNeither()
    {
        var (location, meetingUrl) = SeedTimeline.ResolveSeedVenue(
            SessionKind.AssignmentWindow,
            "CLS-ANY",
            0);

        Assert.Null(location);
        Assert.Null(meetingUrl);
    }
}
