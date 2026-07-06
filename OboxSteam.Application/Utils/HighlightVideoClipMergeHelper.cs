using OboxSteam.Application.Interfaces;

namespace OboxSteam.Application.Utils;

/// <summary>
/// Applies buffer + gap-merge to raw source ranges before formatting MediaConvert timecodes.
/// Used when rendering highlight videos; raw ranges are persisted separately in the manifest.
/// </summary>
public static class HighlightVideoClipMergeHelper
{
    private const long BufferMs = 2_000;
    private const long PointBufferMs = 1_000;
    private const long MergeGapMs = 500;

    public static List<TimeClip> MergeAndFormatToTimeClips(
        IEnumerable<(long StartMs, long EndMs)> segments)
    {
        var bufferedRanges = segments
            .Where(s => s.StartMs <= s.EndMs)
            .Select(s =>
            {
                var buffer = s.StartMs == s.EndMs ? PointBufferMs : BufferMs;
                return (Start: Math.Max(0, s.StartMs - buffer), End: s.EndMs + buffer);
            })
            .Where(r => r.End > r.Start)
            .OrderBy(r => r.Start)
            .ToList();

        if (bufferedRanges.Count == 0)
            return new List<TimeClip>();

        var merged = new List<(long Start, long End)> { bufferedRanges[0] };
        for (var i = 1; i < bufferedRanges.Count; i++)
        {
            var current = bufferedRanges[i];
            var last = merged[^1];
            if (current.Start <= last.End + MergeGapMs)
                merged[^1] = (last.Start, Math.Max(last.End, current.End));
            else
                merged.Add(current);
        }

        return merged
            .Select(r => new TimeClip(
                MsToMediaConvertTimecode(r.Start),
                MsToMediaConvertTimecode(r.End)))
            .Where(t => t.StartTimecode != t.EndTimecode)
            .ToList();
    }

    public static List<TimeClip> MergeAndFormatToTimeClips(
        IReadOnlyList<HighlightSourceSegmentMs> rawSegments)
    {
        var ranges = rawSegments
            .Select(s => (s.StartMs, s.EndMs ?? s.StartMs))
            .ToList();

        return MergeAndFormatToTimeClips(ranges);
    }

    private static string MsToMediaConvertTimecode(long ms)
    {
        var totalSec = (ms + 500) / 1_000;
        var sec = (int)(totalSec % 60);
        var min = (int)(totalSec / 60 % 60);
        var hr = (int)(totalSec / 3_600);
        return $"{hr:D2}:{min:D2}:{sec:D2}:00";
    }
}
