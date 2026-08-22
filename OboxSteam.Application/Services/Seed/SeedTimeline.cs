using OboxSteam.Domain.Enums;

namespace OboxSteam.Application.Services;

/// <summary>
/// Pure date helpers for academic-year seed data.
/// </summary>
public sealed class SeedTimeline
{
    public readonly record struct WeekdaySlot(DayOfWeek Day, int Hour, int Minute, int DurationMinutes);

    public DateTime Now { get; }

    public SeedTimeline(DateTime now)
    {
        Now = now;
    }

    public DateTime AtDays(int days) => Now.AddDays(days);

    public DateTime AtMonths(int months) => Now.AddMonths(months);

    public static ClassSessionStatus ResolveSessionStatus(DateTime startTime, DateTime endTime, DateTime now)
    {
        if (endTime <= now)
        {
            return ClassSessionStatus.Completed;
        }

        if (startTime <= now && now < endTime)
        {
            return ClassSessionStatus.InProgress;
        }

        return ClassSessionStatus.Scheduled;
    }

    public static DateTime AlignToDayOfWeek(DateTime date, DayOfWeek dayOfWeek)
    {
        var delta = ((int)dayOfWeek - (int)date.DayOfWeek + 7) % 7;
        return date.Date.AddDays(delta);
    }

    public static bool RangesOverlap(DateTime start1, DateTime end1, DateTime start2, DateTime end2)
        => start1 < end2 && start2 < end1;

    /// <summary>
    /// Walks the class calendar and returns the sessionIndex-th occurrence of the weekly slots.
    /// Returns null when the window does not contain that many slots.
    /// </summary>
    public static (DateTime StartTime, DateTime EndTime)? TryResolveSlotSequence(
        DateTime classStart,
        DateTime classEnd,
        IReadOnlyList<WeekdaySlot> weeklySlots,
        int sessionIndex)
    {
        if (weeklySlots.Count == 0)
        {
            throw new ArgumentException("At least one weekly slot is required.", nameof(weeklySlots));
        }

        if (sessionIndex < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(sessionIndex));
        }

        var matched = 0;
        for (var date = classStart.Date; date <= classEnd.Date; date = date.AddDays(1))
        {
            foreach (var slot in weeklySlots)
            {
                if (date.DayOfWeek != slot.Day)
                {
                    continue;
                }

                if (matched == sessionIndex)
                {
                    var start = date.AddHours(slot.Hour).AddMinutes(slot.Minute);
                    var end = start.AddMinutes(slot.DurationMinutes);
                    if (end > classEnd.AddDays(1))
                    {
                        return null;
                    }

                    return (start, end);
                }

                matched++;
            }
        }

        return null;
    }

    public static AttendanceStatus AttendanceForIndex(int studentIndex, int sessionIndex)
    {
        var mix = (studentIndex + sessionIndex) % 10;
        return mix switch
        {
            0 => AttendanceStatus.Late,
            1 => AttendanceStatus.Absent,
            2 => AttendanceStatus.Excused,
            _ => AttendanceStatus.Present,
        };
    }

    /// <summary>
    /// Fake venue for seed/FE: Lesson gets campus room + meet URL; FieldTrip gets lab location only.
    /// </summary>
    public static (string? Location, string? MeetingUrl) ResolveSeedVenue(
        SessionKind kind,
        string classCode,
        int ordinal)
    {
        var safeCode = string.IsNullOrWhiteSpace(classCode)
            ? "class"
            : classCode.Trim().ToLowerInvariant();
        var index = Math.Abs(ordinal);
        var room = $"NVH {600 + (index % 20):D3}";
        var lab = $"Campus Lab {(index % 5) + 1}";
        var meetUrl = $"https://meet.oboxsteam.com/{safeCode}/s{index:D2}";

        return kind switch
        {
            SessionKind.Lesson => (room, meetUrl),
            SessionKind.FieldTrip => (lab, null),
            _ => (null, null),
        };
    }
}
