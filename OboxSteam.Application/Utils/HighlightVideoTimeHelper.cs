using System.Globalization;
using System.Text.RegularExpressions;
using OboxSteam.Application.DTOs.MediaDTO;
using OboxSteam.Application.Interfaces;

namespace OboxSteam.Application.Utils;

/// <summary>
/// Parses output-timeline timecodes and computes keep segments from exclude ranges.
/// </summary>
public static partial class HighlightVideoTimeHelper
{
    private const long MsPerSecond = 1_000;
    private const long MsPerMinute = 60 * MsPerSecond;
    private const long MsPerHour = 60 * MsPerMinute;

    /// <summary>
    /// Parses <c>HH:MM:SS</c> or <c>HH:MM:SS.mmm</c> into milliseconds from video start.
    /// </summary>
    public static long ParseTimecodeToMs(string timecode)
    {
        if (string.IsNullOrWhiteSpace(timecode))
            throw ErrorHelper.BadRequest("Time range value cannot be empty.");

        var trimmed = timecode.Trim();
        var match = TimecodeRegex().Match(trimmed);
        if (!match.Success)
            throw ErrorHelper.BadRequest($"Invalid time format '{timecode}'. Use HH:MM:SS or HH:MM:SS.mmm.");

        var hours = int.Parse(match.Groups["h"].Value, CultureInfo.InvariantCulture);
        var minutes = int.Parse(match.Groups["m"].Value, CultureInfo.InvariantCulture);
        var seconds = int.Parse(match.Groups["s"].Value, CultureInfo.InvariantCulture);
        var millis = match.Groups["ms"].Success
            ? int.Parse(match.Groups["ms"].Value, CultureInfo.InvariantCulture)
            : 0;

        if (minutes >= 60 || seconds >= 60)
            throw ErrorHelper.BadRequest($"Invalid time value '{timecode}'.");

        return hours * MsPerHour + minutes * MsPerMinute + seconds * MsPerSecond + millis;
    }

    /// <summary>Formats milliseconds as MediaConvert timecode <c>HH:MM:SS:00</c> (truncates sub-second).</summary>
    public static string MsToMediaConvertTimecode(long ms)
    {
        if (ms < 0)
            ms = 0;

        var hours = ms / MsPerHour;
        ms %= MsPerHour;
        var minutes = ms / MsPerMinute;
        ms %= MsPerMinute;
        var seconds = ms / MsPerSecond;

        return $"{hours:D2}:{minutes:D2}:{seconds:D2}:00";
    }

    /// <summary>
    /// Formats milliseconds as MediaConvert timecode, rounding to the nearest second.
    /// Used when encoding source clips with buffer/merge so clip bounds align with MediaConvert.
    /// </summary>
    public static string MsToMediaConvertTimecodeRounded(long ms)
    {
        if (ms < 0)
            ms = 0;

        var totalSec = (ms + 500) / MsPerSecond;
        var sec = (int)(totalSec % 60);
        var min = (int)(totalSec / 60 % 60);
        var hr = (int)(totalSec / 3_600);
        return $"{hr:D2}:{min:D2}:{sec:D2}:00";
    }

    /// <summary>Parses UI exclude ranges into millisecond tuples.</summary>
    public static List<(long StartMs, long EndMs)> ParseExcludeRanges(
        IEnumerable<TimeRangeDto> excludeRanges) =>
        excludeRanges
            .Select(r => (StartMs: ParseTimecodeToMs(r.Start), EndMs: ParseTimecodeToMs(r.End)))
            .ToList();

    /// <summary>
    /// Normalizes exclude ranges to [0, durationMs] and merges overlaps/adjacent spans.
    /// </summary>
    public static IReadOnlyList<(long StartMs, long EndMs)> NormalizeMergedExcludeRanges(
        long durationMs,
        IReadOnlyList<(long StartMs, long EndMs)> excludeRanges)
    {
        if (durationMs <= 0)
            throw ErrorHelper.BadRequest("Video duration is unknown; cannot trim until generation completes.");

        if (excludeRanges.Count == 0)
            throw ErrorHelper.BadRequest("At least one exclude range is required for trimming.");

        var mergedExcludes = MergeRanges(
            excludeRanges
                .Select(r =>
                {
                    var start = Math.Clamp(r.StartMs, 0, durationMs);
                    var end = Math.Clamp(r.EndMs, 0, durationMs);
                    if (end < start)
                        (start, end) = (end, start);
                    return (StartMs: start, EndMs: end);
                })
                .Where(r => r.EndMs > r.StartMs)
                .ToList());

        if (mergedExcludes.Count == 0)
            throw ErrorHelper.BadRequest("Exclude ranges are invalid or empty after normalization.");

        return mergedExcludes;
    }

    /// <summary>
    /// Computes keep segments as the complement of exclude ranges within [0, durationMs].
    /// </summary>
    public static IReadOnlyList<(long StartMs, long EndMs)> ComputeKeepSegments(
        long durationMs,
        IReadOnlyList<(long StartMs, long EndMs)> excludeRanges)
    {
        var mergedExcludes = NormalizeMergedExcludeRanges(durationMs, excludeRanges);

        var keep = new List<(long StartMs, long EndMs)>();
        var cursor = 0L;

        foreach (var (start, end) in mergedExcludes)
        {
            if (start > cursor)
                keep.Add((cursor, start));
            cursor = Math.Max(cursor, end);
        }

        if (cursor < durationMs)
            keep.Add((cursor, durationMs));

        if (keep.Count == 0)
            throw ErrorHelper.BadRequest("Trim would remove the entire video. Adjust exclude ranges.");

        return keep;
    }

    public static List<TimeClip> ToTimeClips(IReadOnlyList<(long StartMs, long EndMs)> keepSegments) =>
        keepSegments
            .Where(s => s.EndMs > s.StartMs)
            .Select(s => new TimeClip(
                MsToMediaConvertTimecode(s.StartMs),
                MsToMediaConvertTimecode(s.EndMs)))
            .ToList();

    private static List<(long StartMs, long EndMs)> MergeRanges(List<(long StartMs, long EndMs)> ranges)
    {
        if (ranges.Count == 0)
            return ranges;

        var sorted = ranges.OrderBy(r => r.StartMs).ToList();
        var merged = new List<(long StartMs, long EndMs)> { sorted[0] };

        for (var i = 1; i < sorted.Count; i++)
        {
            var last = merged[^1];
            var current = sorted[i];
            if (current.StartMs <= last.EndMs)
            {
                merged[^1] = (last.StartMs, Math.Max(last.EndMs, current.EndMs));
            }
            else
            {
                merged.Add(current);
            }
        }

        return merged;
    }

    [GeneratedRegex(@"^(?<h>\d{1,2}):(?<m>\d{2}):(?<s>\d{2})(?:\.(?<ms>\d{1,3}))?$", RegexOptions.CultureInvariant)]
    private static partial Regex TimecodeRegex();
}
