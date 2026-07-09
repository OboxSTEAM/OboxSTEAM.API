using OboxSteam.Application.Interfaces;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace OboxSteam.Application.Utils;

/// <summary>
/// Render-clip manifest for highlight videos (version 3).
/// Each segment's <c>startMs</c>/<c>endMs</c> is the exact source range sent to MediaConvert
/// (already buffered/merged). Raw face ranges are never persisted.
/// </summary>
public static class HighlightVideoManifestHelper
{
    public const int RenderManifestVersion = 3;

    /// <summary>Legacy: raw face ranges; merge/buffer applied at encode time.</summary>
    public const int RawManifestVersion = 2;

    /// <summary>Legacy: array-only JSON without a version wrapper.</summary>
    public const int LegacyMergedManifestVersion = 1;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public static string SerializeManifest(IReadOnlyList<HighlightSourceClipGroup> groups) =>
        JsonSerializer.Serialize(
            new HighlightSourceManifestDocument(RenderManifestVersion, SanitizeManifest(groups)),
            JsonOpts);

    public static List<HighlightSourceClipGroup> DeserializeManifest(string json) =>
        ParseManifest(json).Groups;

    public static ParsedHighlightManifest ParseManifest(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            throw new InvalidOperationException("Source segment manifest is empty.");

        var trimmed = json.TrimStart();
        if (trimmed.StartsWith('['))
        {
            var legacy = JsonSerializer.Deserialize<List<HighlightSourceClipGroup>>(json, JsonOpts)
                         ?? throw new InvalidOperationException("Failed to deserialize source segment manifest.");
            return new ParsedHighlightManifest(
                LegacyMergedManifestVersion,
                MaterializeLegacyToRenderClips(SanitizeManifest(legacy)));
        }

        var document = JsonSerializer.Deserialize<HighlightSourceManifestDocument>(json, JsonOpts)
                       ?? throw new InvalidOperationException("Failed to deserialize source segment manifest.");

        var version = document.Version <= 0 ? LegacyMergedManifestVersion : document.Version;
        var groups = SanitizeManifest(
            document.Groups?.ToList()
            ?? throw new InvalidOperationException("Source segment manifest has no groups."));

        if (version < RenderManifestVersion)
            groups = MaterializeLegacyToRenderClips(groups);

        return new ParsedHighlightManifest(RenderManifestVersion, groups);
    }

    /// <summary>
    /// Builds a render manifest from MediaConvert clip inputs (post buffer/merge).
    /// </summary>
    public static List<HighlightSourceClipGroup> BuildFromRenderClipInputs(
        IReadOnlyList<HighlightClipMediaPair> clipsWithMedia)
    {
        var groups = new List<HighlightSourceClipGroup>();

        foreach (var pair in clipsWithMedia)
        {
            var segments = pair.Clip.Clips is { Count: > 0 }
                ? pair.Clip.Clips
                    .Select(c => new HighlightSourceSegmentMs(
                        ParseMediaConvertTimecode(c.StartTimecode),
                        ParseMediaConvertTimecode(c.EndTimecode)))
                    .Where(s => s.EndMs is null || s.EndMs > s.StartMs)
                    .ToList()
                : new List<HighlightSourceSegmentMs> { new(0, null) };

            groups.Add(new HighlightSourceClipGroup(pair.MediaId, pair.Clip.S3Key, segments));
        }

        return SanitizeManifest(groups);
    }

    /// <summary>
    /// Buffers a user-selected source range once, then appends the resulting render clip(s).
    /// </summary>
    public static List<HighlightSourceClipGroup> AppendBufferedSegment(
        IReadOnlyList<HighlightSourceClipGroup> manifest,
        Guid mediaId,
        string sourceS3Key,
        long startMs,
        long endMs)
    {
        if (startMs < 0 || endMs <= startMs)
            throw new ArgumentException("Segment startMs/endMs are invalid.");

        var bufferedClips = HighlightVideoClipMergeHelper.MergeAndFormatToTimeClips(
            new[] { (startMs, endMs) });
        if (bufferedClips.Count == 0)
            throw new ArgumentException("Segment produced no render clips after buffering.");

        var newSegments = bufferedClips
            .Select(c => new HighlightSourceSegmentMs(
                ParseMediaConvertTimecode(c.StartTimecode),
                ParseMediaConvertTimecode(c.EndTimecode)))
            .ToList();

        return InsertSegmentsIntoManifest(manifest, mediaId, sourceS3Key, newSegments);
    }

    /// <summary>
    /// Cuts render clips using exclude ranges on the stamped output timeline.
    /// Resulting <c>startMs</c>/<c>endMs</c> remain render bounds (no extra buffer).
    /// </summary>
    public static List<HighlightSourceClipGroup> ApplyOutputTrim(
        IReadOnlyList<HighlightSourceClipGroup> manifest,
        long outputDurationMs,
        IReadOnlyList<(long StartMs, long EndMs)> excludeRanges,
        IReadOnlyDictionary<Guid, long> sourceDurationMsByMediaId)
    {
        if (!HasStampedOutputTimeline(manifest))
            throw new InvalidOperationException(
                "Output timeline stamps are required before trimming. Wait for generation to complete.");

        var mergedExcludes = HighlightVideoTimeHelper.NormalizeMergedExcludeRanges(
            outputDurationMs, excludeRanges);
        _ = HighlightVideoTimeHelper.ComputeKeepSegments(outputDurationMs, excludeRanges);
        var s3KeyByMediaId = manifest.ToDictionary(g => g.MediaId, g => g.SourceS3Key);
        var pieces = new List<SourcePiece>();

        foreach (var group in manifest)
        {
            foreach (var segment in group.Segments)
            {
                var sourceEnd = ResolveSourceEndMs(group.MediaId, segment, sourceDurationMsByMediaId);
                var outputStart = segment.OutputStartMs
                    ?? throw new InvalidOperationException("Segment is missing outputStartMs.");
                var outputEnd = segment.OutputEndMs
                    ?? throw new InvalidOperationException("Segment is missing outputEndMs.");

                foreach (var (keepStart, keepEnd) in SubtractExcludesFromOutputRange(
                             outputStart, outputEnd, mergedExcludes))
                {
                    var outputSpanMs = outputEnd - outputStart;
                    var sourceSpanMs = sourceEnd - segment.StartMs;
                    if (outputSpanMs <= 0 || sourceSpanMs <= 0)
                        continue;

                    var sourceStart = segment.StartMs
                        + (keepStart - outputStart) * sourceSpanMs / outputSpanMs;
                    var mappedSourceEnd = segment.StartMs
                        + (keepEnd - outputStart) * sourceSpanMs / outputSpanMs;
                    if (mappedSourceEnd <= sourceStart)
                        continue;

                    pieces.Add(new SourcePiece(
                        group.MediaId,
                        s3KeyByMediaId[group.MediaId],
                        sourceStart,
                        mappedSourceEnd,
                        keepStart,
                        keepEnd));
                }
            }
        }

        var transformed = RebuildManifestFromPieces(pieces, manifest);
        ValidateSegmentOrder(transformed);
        return transformed;
    }

    public static void ValidateSegmentOrder(IReadOnlyList<HighlightSourceClipGroup> manifest)
    {
        foreach (var group in manifest)
        {
            if (group.Segments.Count == 0)
                throw new InvalidOperationException($"Source clip {group.MediaId} has no segments.");

            foreach (var seg in group.Segments)
            {
                if (seg.StartMs < 0)
                    throw new InvalidOperationException("Segment startMs must be non-negative.");

                if (seg.EndMs.HasValue && seg.EndMs.Value <= seg.StartMs)
                    throw new InvalidOperationException("Segment endMs must be greater than startMs.");
            }
        }
    }

    public static bool SegmentOverlapsExisting(
        IReadOnlyList<HighlightSourceClipGroup> manifest,
        Guid mediaId,
        long startMs,
        long endMs)
    {
        foreach (var group in manifest.Where(g => g.MediaId == mediaId))
        {
            foreach (var segment in group.Segments)
            {
                if (SourceRangesOverlap(startMs, endMs, segment))
                    return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Converts the render manifest into MediaConvert inputs. Always uses segment bounds directly
    /// (no buffer/merge) — buffering happens only when building the initial list or adding a segment.
    /// </summary>
    public static List<ClipInput> ToClipInputs(IReadOnlyList<HighlightSourceClipGroup> manifest)
    {
        var clipInputs = new List<ClipInput>();

        foreach (var group in SanitizeManifest(manifest))
        {
            ValidateSegmentOrder(new[] { group });

            if (IsFullVideoGroup(group))
            {
                clipInputs.Add(new ClipInput(group.SourceS3Key, new List<TimeClip>()));
                continue;
            }

            clipInputs.Add(new ClipInput(group.SourceS3Key, FormatSegmentsDirectly(group.Segments)));
        }

        return clipInputs;
    }

    /// <summary>
    /// Stamps each render segment with its span on the stitched output timeline.
    /// </summary>
    public static List<HighlightSourceClipGroup> StampOutputTimeline(
        IReadOnlyList<HighlightSourceClipGroup> manifest,
        long actualOutputDurationMs,
        IReadOnlyDictionary<Guid, long>? sourceDurationMsByMediaId = null)
    {
        if (actualOutputDurationMs <= 0)
            throw new ArgumentOutOfRangeException(nameof(actualOutputDurationMs));

        var sanitized = SanitizeManifest(manifest);
        var flat = FlattenSegments(sanitized);
        if (flat.Count == 0)
            return sanitized;

        var weights = flat
            .Select(e => Math.Max(
                1L,
                ResolveSourceEndMs(e.Group.MediaId, e.Segment, sourceDurationMsByMediaId) - e.Segment.StartMs))
            .ToList();
        var totalWeight = weights.Sum();
        if (totalWeight <= 0)
            throw new InvalidOperationException("Cannot stamp output timeline: no segment weights.");

        long cursor = 0;
        var resultGroups = sanitized
            .Select(g => g with
            {
                Segments = g.Segments
                    .Select(s => new HighlightSourceSegmentMs(s.StartMs, s.EndMs, s.OutputStartMs, s.OutputEndMs))
                    .ToList()
            })
            .ToList();

        for (var i = 0; i < flat.Count; i++)
        {
            var entry = flat[i];
            long outputEnd;
            if (i == flat.Count - 1)
            {
                outputEnd = actualOutputDurationMs;
            }
            else
            {
                outputEnd = cursor + weights[i] * actualOutputDurationMs / totalWeight;
                if (outputEnd <= cursor)
                    outputEnd = cursor + 1;
            }

            var groupIndex = resultGroups.FindIndex(g => g.MediaId == entry.Group.MediaId);
            var segments = resultGroups[groupIndex].Segments.ToList();
            var seg = segments[entry.SegmentIndex];
            segments[entry.SegmentIndex] = new HighlightSourceSegmentMs(
                seg.StartMs,
                ResolveStampedSourceEndMs(entry.Group.MediaId, seg, sourceDurationMsByMediaId),
                cursor,
                outputEnd);
            resultGroups[groupIndex] = resultGroups[groupIndex] with { Segments = segments };
            cursor = outputEnd;
        }

        return resultGroups;
    }

    public static bool HasStampedOutputTimeline(IReadOnlyList<HighlightSourceClipGroup> manifest) =>
        manifest.Count > 0
        && manifest.SelectMany(g => g.Segments).All(IsStampedOutputSegment);

    public static bool IsStampedOutputSegment(HighlightSourceSegmentMs segment) =>
        segment.OutputStartMs is >= 0 && segment.OutputEndMs is > 0;

    public static List<HighlightSourceClipGroup> SanitizeManifest(
        IReadOnlyList<HighlightSourceClipGroup> manifest)
    {
        var result = new List<HighlightSourceClipGroup>();

        foreach (var group in manifest)
        {
            var segments = FilterDegenerateSegments(group.Segments);
            if (segments.Count > 0)
                result.Add(group with { Segments = segments });
        }

        return result;
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    /// <summary>
    /// Converts legacy v1/v2 (raw or pre-merged) segments into render clips by applying
    /// the same buffer/merge used at encode time. When output stamps exist, each merged
    /// clip inherits the union of overlapping stamped output spans so trim still works.
    /// </summary>
    private static List<HighlightSourceClipGroup> MaterializeLegacyToRenderClips(
        IReadOnlyList<HighlightSourceClipGroup> legacy)
    {
        var result = new List<HighlightSourceClipGroup>();

        foreach (var group in legacy)
        {
            if (IsFullVideoGroup(group))
            {
                var full = group.Segments[0];
                result.Add(new HighlightSourceClipGroup(
                    group.MediaId,
                    group.SourceS3Key,
                    new List<HighlightSourceSegmentMs>
                    {
                        new(0, null, full.OutputStartMs, full.OutputEndMs)
                    }));
                continue;
            }

            var timeClips = HighlightVideoClipMergeHelper.MergeAndFormatToTimeClips(group.Segments);
            if (timeClips.Count == 0)
                continue;

            var segments = new List<HighlightSourceSegmentMs>();
            foreach (var clip in timeClips)
            {
                var startMs = ParseMediaConvertTimecode(clip.StartTimecode);
                var endMs = ParseMediaConvertTimecode(clip.EndTimecode);
                long? outputStart = null;
                long? outputEnd = null;

                var overlapping = group.Segments
                    .Where(s => IsStampedOutputSegment(s)
                                && SourceRangesOverlap(startMs, endMs, s))
                    .ToList();
                if (overlapping.Count > 0)
                {
                    outputStart = overlapping.Min(s => s.OutputStartMs!.Value);
                    outputEnd = overlapping.Max(s => s.OutputEndMs!.Value);
                }

                segments.Add(new HighlightSourceSegmentMs(startMs, endMs, outputStart, outputEnd));
            }

            result.Add(new HighlightSourceClipGroup(group.MediaId, group.SourceS3Key, segments));
        }

        return SanitizeManifest(result);
    }

    private static List<HighlightSourceClipGroup> InsertSegmentsIntoManifest(
        IReadOnlyList<HighlightSourceClipGroup> manifest,
        Guid mediaId,
        string sourceS3Key,
        IReadOnlyList<HighlightSourceSegmentMs> newSegments)
    {
        var result = manifest
            .Select(CloneGroup)
            .ToList();

        var groupIndex = result.FindIndex(g => g.MediaId == mediaId);
        if (groupIndex >= 0)
        {
            var group = result[groupIndex];
            if (!string.Equals(group.SourceS3Key, sourceS3Key, StringComparison.Ordinal))
            {
                throw new ArgumentException(
                    $"Source S3 key does not match the existing clip group for media {mediaId}.");
            }

            var segments = group.Segments.ToList();
            segments.AddRange(newSegments);
            result[groupIndex] = group with { Segments = SortSegmentsByStartMs(segments) };
            return SanitizeManifest(result);
        }

        result.Add(new HighlightSourceClipGroup(mediaId, sourceS3Key, SortSegmentsByStartMs(newSegments)));
        return SanitizeManifest(result);
    }

    private static HighlightSourceClipGroup CloneGroup(HighlightSourceClipGroup group) =>
        group with
        {
            Segments = group.Segments
                .Select(s => new HighlightSourceSegmentMs(
                    s.StartMs,
                    s.EndMs,
                    s.OutputStartMs,
                    s.OutputEndMs))
                .ToList()
        };

    private static List<HighlightSourceSegmentMs> SortSegmentsByStartMs(
        IEnumerable<HighlightSourceSegmentMs> segments) =>
        segments.OrderBy(s => s.StartMs).ToList();

    private static List<HighlightSourceSegmentMs> FilterDegenerateSegments(
        IEnumerable<HighlightSourceSegmentMs> segments) =>
        segments
            .Where(s => !s.EndMs.HasValue || s.EndMs.Value > s.StartMs)
            .ToList();

    private static bool SourceRangesOverlap(long startMs, long endMs, HighlightSourceSegmentMs existing)
    {
        if (existing.StartMs == 0 && existing.EndMs is null)
            return true;

        var existingEnd = existing.EndMs ?? existing.StartMs;
        return startMs < existingEnd && existing.StartMs < endMs;
    }

    private static bool IsFullVideoGroup(HighlightSourceClipGroup group) =>
        group.Segments.Count == 1
        && group.Segments[0].StartMs == 0
        && group.Segments[0].EndMs is null;

    private static List<TimeClip> FormatSegmentsDirectly(IReadOnlyList<HighlightSourceSegmentMs> segments) =>
        segments
            .Where(s => s.EndMs is > 0)
            .Select(s => new TimeClip(
                HighlightVideoTimeHelper.MsToMediaConvertTimecodeRounded(s.StartMs),
                HighlightVideoTimeHelper.MsToMediaConvertTimecodeRounded(s.EndMs!.Value)))
            .Where(t => t.StartTimecode != t.EndTimecode)
            .ToList();

    private static List<(long StartMs, long EndMs)> SubtractExcludesFromOutputRange(
        long rangeStartMs,
        long rangeEndMs,
        IReadOnlyList<(long StartMs, long EndMs)> mergedExcludes)
    {
        var kept = new List<(long StartMs, long EndMs)> { (rangeStartMs, rangeEndMs) };

        foreach (var (excludeStart, excludeEnd) in mergedExcludes)
        {
            var next = new List<(long StartMs, long EndMs)>();
            foreach (var (keepStart, keepEnd) in kept)
            {
                if (excludeEnd <= keepStart || excludeStart >= keepEnd)
                {
                    next.Add((keepStart, keepEnd));
                    continue;
                }

                if (excludeStart > keepStart)
                    next.Add((keepStart, excludeStart));
                if (excludeEnd < keepEnd)
                    next.Add((excludeEnd, keepEnd));
            }

            kept = next;
        }

        return kept.Where(r => r.EndMs > r.StartMs).ToList();
    }

    private static List<HighlightSourceClipGroup> RebuildManifestFromPieces(
        IReadOnlyList<SourcePiece> pieces,
        IReadOnlyList<HighlightSourceClipGroup> originalManifest)
    {
        if (pieces.Count == 0)
            throw new InvalidOperationException("Trim would remove all source segments from the manifest.");

        var mediaOrder = originalManifest.Select(g => g.MediaId).Distinct().ToList();
        var result = new List<HighlightSourceClipGroup>();

        foreach (var mediaId in mediaOrder)
        {
            var groupPieces = pieces
                .Where(p => p.MediaId == mediaId)
                .OrderBy(p => p.SourceStartMs)
                .ToList();

            if (groupPieces.Count == 0)
                continue;

            var sourceS3Key = originalManifest.First(g => g.MediaId == mediaId).SourceS3Key;
            var segments = groupPieces
                .Select(p => new HighlightSourceSegmentMs(
                    p.SourceStartMs,
                    p.SourceEndMs,
                    p.OutputStartMs,
                    p.OutputEndMs))
                .ToList();

            result.Add(new HighlightSourceClipGroup(mediaId, sourceS3Key, segments));
        }

        if (result.Count == 0)
            throw new InvalidOperationException("Trim would remove all source segments from the manifest.");

        return result;
    }

    private static List<FlatSegmentEntry> FlattenSegments(IReadOnlyList<HighlightSourceClipGroup> manifest)
    {
        var flat = new List<FlatSegmentEntry>();
        foreach (var group in manifest)
        {
            for (var i = 0; i < group.Segments.Count; i++)
                flat.Add(new FlatSegmentEntry(group, i, group.Segments[i]));
        }

        return flat;
    }

    private static long ResolveSourceEndMs(
        Guid mediaId,
        HighlightSourceSegmentMs segment,
        IReadOnlyDictionary<Guid, long>? sourceDurationMsByMediaId)
    {
        if (segment.EndMs is > 0)
            return segment.EndMs.Value;

        if (sourceDurationMsByMediaId != null
            && sourceDurationMsByMediaId.TryGetValue(mediaId, out var duration)
            && duration > segment.StartMs)
        {
            return duration;
        }

        throw new InvalidOperationException(
            $"Cannot resolve source end for media {mediaId}; endMs and source duration are missing.");
    }

    private static long ResolveStampedSourceEndMs(
        Guid mediaId,
        HighlightSourceSegmentMs segment,
        IReadOnlyDictionary<Guid, long>? sourceDurationMsByMediaId)
    {
        if (segment.EndMs is > 0)
            return segment.EndMs.Value;

        return ResolveSourceEndMs(mediaId, segment, sourceDurationMsByMediaId);
    }

    private static long ParseMediaConvertTimecode(string timecode)
    {
        if (string.IsNullOrWhiteSpace(timecode))
            return 0;

        var parts = timecode.Trim().Split(':');
        if (parts.Length < 3)
            throw new FormatException($"Invalid MediaConvert timecode '{timecode}'.");

        var hours = int.Parse(parts[0], CultureInfo.InvariantCulture);
        var minutes = int.Parse(parts[1], CultureInfo.InvariantCulture);
        var seconds = int.Parse(parts[2], CultureInfo.InvariantCulture);

        return (hours * 3_600L + minutes * 60L + seconds) * 1_000L;
    }

    private sealed record FlatSegmentEntry(
        HighlightSourceClipGroup Group,
        int SegmentIndex,
        HighlightSourceSegmentMs Segment);

    private sealed record SourcePiece(
        Guid MediaId,
        string SourceS3Key,
        long SourceStartMs,
        long SourceEndMs,
        long OutputStartMs,
        long OutputEndMs);
}

public sealed record HighlightClipMediaPair(
    Guid MediaId,
    DateTime MediaCreatedAt,
    ClipInput Clip);

public sealed record ParsedHighlightManifest(
    int Version,
    List<HighlightSourceClipGroup> Groups);

public sealed record HighlightSourceManifestDocument(
    int Version,
    IReadOnlyList<HighlightSourceClipGroup> Groups);

public sealed record HighlightSourceSegmentMs(
    long StartMs,
    long? EndMs,
    long? OutputStartMs = null,
    long? OutputEndMs = null);

public sealed record HighlightSourceClipGroup(
    Guid MediaId,
    string SourceS3Key,
    IReadOnlyList<HighlightSourceSegmentMs> Segments);
