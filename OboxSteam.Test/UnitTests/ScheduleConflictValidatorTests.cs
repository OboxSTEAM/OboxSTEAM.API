using OboxSteam.Application.Validation;
using OboxSteam.Domain.Entities;
using OboxSteam.Domain.Enums;

namespace OboxSteam.Test.UnitTests;

public sealed class ScheduleConflictValidatorTests
{
    [Fact]
    public void Overlaps_IsTrue_WhenIntervalsIntersect()
    {
        var start = new DateTime(2026, 8, 22, 9, 0, 0, DateTimeKind.Utc);
        var end = start.AddHours(2);

        Assert.True(ScheduleConflictValidator.Overlaps(start, end, start.AddHours(1), end.AddHours(1)));
        Assert.True(ScheduleConflictValidator.Overlaps(start, end, start.AddMinutes(-30), start.AddMinutes(30)));
    }

    [Fact]
    public void Overlaps_IsFalse_WhenIntervalsAreAdjacentOrDisjoint()
    {
        var start = new DateTime(2026, 8, 22, 9, 0, 0, DateTimeKind.Utc);
        var end = start.AddHours(2);

        Assert.False(ScheduleConflictValidator.Overlaps(start, end, end, end.AddHours(2)));
        Assert.False(ScheduleConflictValidator.Overlaps(start, end, end.AddHours(1), end.AddHours(3)));
    }

    [Fact]
    public void FindFirstOverlap_IgnoresCancelledSessions()
    {
        var start = new DateTime(2026, 8, 22, 9, 0, 0, DateTimeKind.Utc);
        var busy = new List<ClassSession>
        {
            new()
            {
                Title = "Cancelled busy",
                StartTime = start,
                EndTime = start.AddHours(2),
                Status = ClassSessionStatus.Cancelled,
            },
        };
        var candidates = new List<ClassSession>
        {
            new()
            {
                Title = "Candidate",
                StartTime = start,
                EndTime = start.AddHours(2),
                Status = ClassSessionStatus.Scheduled,
            },
        };

        Assert.Null(ScheduleConflictValidator.FindFirstOverlap(busy, candidates));
    }

    [Fact]
    public void FindFirstOverlap_ReturnsBusySession_WhenTimesOverlap()
    {
        var start = new DateTime(2026, 8, 22, 9, 0, 0, DateTimeKind.Utc);
        var busy = new ClassSession
        {
            Title = "Busy",
            StartTime = start,
            EndTime = start.AddHours(2),
            Status = ClassSessionStatus.Scheduled,
        };
        var candidates = new List<ClassSession>
        {
            new()
            {
                Title = "Candidate",
                StartTime = start.AddHours(1),
                EndTime = start.AddHours(3),
                Status = ClassSessionStatus.InProgress,
            },
        };

        var found = ScheduleConflictValidator.FindFirstOverlap([busy], candidates);

        Assert.Same(busy, found);
    }
}
