using OboxSteam.Application.Validation;
using OboxSteam.Domain.Enums;

namespace OboxSteam.Application.Services;

/// <summary>
/// Pure date helpers for academic-year seed data.
/// Session wall-clock times are Asia/Ho_Chi_Minh; stored values are UTC
/// (same contract as <c>GET /api/schedules/weekly</c>).
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
    /// Walks the class calendar in Asia/Ho_Chi_Minh and returns the sessionIndex-th
    /// occurrence of the weekly slots as UTC. Hour/Minute on <see cref="WeekdaySlot"/>
    /// are local Vietnam wall-clock times.
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

        var vietnam = ResolveVietnamTimeZone();
        var startLocal = TimeZoneInfo.ConvertTimeFromUtc(AsUtc(classStart), vietnam);
        var endLocal = TimeZoneInfo.ConvertTimeFromUtc(AsUtc(classEnd), vietnam);
        var startDate = DateOnly.FromDateTime(startLocal);
        var endDate = DateOnly.FromDateTime(endLocal);
        var classEndUtc = AsUtc(classEnd);

        var matched = 0;
        for (var date = startDate; date <= endDate; date = date.AddDays(1))
        {
            foreach (var slot in weeklySlots)
            {
                if (date.DayOfWeek != slot.Day)
                {
                    continue;
                }

                if (matched == sessionIndex)
                {
                    var startUtc = ToUtc(date, new TimeOnly(slot.Hour, slot.Minute), vietnam);
                    var endUtc = startUtc.AddMinutes(slot.DurationMinutes);
                    if (endUtc > classEndUtc.AddDays(1))
                    {
                        return null;
                    }

                    return (startUtc, endUtc);
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
    /// Fake venue for seed/FE: LiveOnline gets campus room + meet URL;
    /// Offline gets lab location + paired Lat/Lng near Ho Chi Minh City campus.
    /// </summary>
    public static (string? Location, string? MeetingUrl, double? Latitude, double? Longitude) ResolveSeedVenue(
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
        // Slight offsets so each Offline slot has a distinct pin (Thu Duc / NVH area).
        var latitude = 10.870000 + (index % 5) * 0.0015;
        var longitude = 106.803000 + (index % 5) * 0.0012;

        return kind switch
        {
            SessionKind.LiveOnline => (room, meetUrl, null, null),
            SessionKind.Offline => (lab, null, latitude, longitude),
            _ => (null, null, null, null),
        };
    }

    public static TimeZoneInfo ResolveVietnamTimeZone()
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(ScheduleValidator.TimezoneId);
        }
        catch (TimeZoneNotFoundException)
        {
            return TimeZoneInfo.FindSystemTimeZoneById(ScheduleValidator.WindowsTimezoneId);
        }
        catch (InvalidTimeZoneException)
        {
            return TimeZoneInfo.FindSystemTimeZoneById(ScheduleValidator.WindowsTimezoneId);
        }
    }

    public static DateTime ToUtc(DateOnly date, TimeOnly time, TimeZoneInfo vietnam)
    {
        var unspecified = DateTime.SpecifyKind(date.ToDateTime(time), DateTimeKind.Unspecified);
        return TimeZoneInfo.ConvertTimeToUtc(unspecified, vietnam);
    }

    public static DateTime AsUtc(DateTime value)
        => value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc),
        };
}
