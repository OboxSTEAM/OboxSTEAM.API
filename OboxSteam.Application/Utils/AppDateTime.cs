using System.Globalization;
using OboxSteam.Application.Validation;

namespace OboxSteam.Application.Utils;

/// <summary>
/// Product datetime contract: store UTC; user wall-clock is Asia/Ho_Chi_Minh.
/// Legacy naive strings (dd/MM/yyyy…) are interpreted as Vietnam local time.
/// Prefer ISO 8601 with offset or Z on the wire.
/// </summary>
public static class AppDateTime
{
    private static readonly string[] LegacyFormats =
    [
        "dd/MM/yyyy HH:mm:ss",
        "dd/MM/yyyy HH:mm",
        "dd/MM/yyyy"
    ];

    public static TimeZoneInfo VietnamTimeZone
    {
        get
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
    }

    /// <summary>
    /// Converts an Asia/Ho_Chi_Minh wall-clock instant to UTC.
    /// </summary>
    public static DateTime VietnamWallClockToUtc(DateTime value)
    {
        var unspecified = DateTime.SpecifyKind(value, DateTimeKind.Unspecified);
        return TimeZoneInfo.ConvertTimeToUtc(unspecified, VietnamTimeZone);
    }

    /// <summary>
    /// Parses API datetime strings into UTC. Legacy dd/MM/yyyy forms are Vietnam wall-clock;
    /// ISO with offset or Z uses the embedded offset; ISO without offset is Vietnam wall-clock.
    /// </summary>
    public static bool TryParseFlexible(string value, out DateTime result)
    {
        if (DateTime.TryParseExact(
                value,
                LegacyFormats,
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out result))
        {
            result = VietnamWallClockToUtc(result);
            return true;
        }

        // Offset / Z present: trust the wire offset (machine-TZ independent).
        if (HasExplicitOffset(value)
            && DateTimeOffset.TryParse(
                value,
                CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind,
                out var withOffset))
        {
            result = withOffset.UtcDateTime;
            return true;
        }

        // Naive ISO (or other round-trip parse without offset): Vietnam wall-clock.
        if (DateTime.TryParse(
                value,
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out result))
        {
            result = VietnamWallClockToUtc(result);
            return true;
        }

        result = default;
        return false;
    }

    /// <summary>
    /// Normalizes persisted UTC instants for comparisons. Database reads may return
    /// <see cref="DateTimeKind.Unspecified"/> even for <c>timestamptz</c> columns.
    /// </summary>
    public static DateTime AsUtc(DateTime value)
        => value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc),
        };

    public static DateTimeOffset ToUtcOffset(DateTime value)
        => new(AsUtc(value), TimeSpan.Zero);

    /// <summary>Formats a UTC instant as <c>HH:mm</c> in Asia/Ho_Chi_Minh.</summary>
    public static string FormatVietnamClock(DateTime utcInstant)
    {
        var local = TimeZoneInfo.ConvertTimeFromUtc(AsUtc(utcInstant), VietnamTimeZone);
        return local.ToString("HH:mm", CultureInfo.InvariantCulture);
    }

    private static bool HasExplicitOffset(string value)
    {
        if (value.EndsWith("Z", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        // Time portion with +HH:mm / -HH:mm (skip date separators like 2026-08-22).
        var timeSep = value.IndexOf('T');
        if (timeSep < 0)
        {
            timeSep = value.IndexOf(' ');
        }

        if (timeSep < 0)
        {
            return false;
        }

        var timePart = value.AsSpan(timeSep + 1);
        return timePart.Contains('+')
               || timePart.LastIndexOf('-') > timePart.IndexOf(':');
    }
}
