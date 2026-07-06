using OboxSteam.Application.Interfaces;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace OboxSteam.Application.Utils;

/// <summary>
/// Source-segment manifest for highlight videos: grouped by mediaId, ordered by
/// <see cref="MediaAssetCreatedAt"/> at the top level and by <c>startMs</c> within each group.
/// </summary>
public static class HighlightVideoManifestHelper
{
    public const int RawManifestVersion = 2;
    public const int LegacyMergedManifestVersion = 1;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public static string SerializeManifest(IReadOnlyList<HighlightSourceClipGroup> groups) =>
        JsonSerializer.Serialize(new HighlightSourceManifestDocument(RawManifestVersion, groups), JsonOpts);

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
            return new ParsedHighlightManifest(LegacyMergedManifestVersion, SanitizeManifest(legacy));
        }

        var document = JsonSerializer.Deserialize<HighlightSourceManifestDocument>(json, JsonOpts)
                       ?? throw new InvalidOperationException("Failed to deserialize source segment manifest.");

        return new ParsedHighlightManifest(
            document.Version <= 0 ? LegacyMergedManifestVersion : document.Version,
            SanitizeManifest(
                document.Groups?.ToList()
                ?? throw new InvalidOperationException("Source segment manifest has no groups.")));
    }

    /// <summary>
    /// Builds manifest groups from pre-merge/pre-buffer source ranges captured during generation.
    /// </summary>
    public static List<HighlightSourceClipGroup> BuildFromRawSourceSegments(
        IReadOnlyList<HighlightClipMediaPair> clipsWithMedia)
    {
        var groups = new List<HighlightSourceClipGroup>();

        foreach (var pair in clipsWithMedia)
        {
            var segments = pair.RawSourceSegments.Count > 0
                ? pair.RawSourceSegments.ToList()
                : new List<HighlightSourceSegmentMs> { new(0, null) };

            groups.Add(new HighlightSourceClipGroup(
                pair.MediaId,
                pair.Clip.S3Key,
                FilterDegenerateSegments(segments)));
        }

        return SanitizeManifest(groups);
    }

    /// <summary>
    /// Inserts a user-selected source segment into the manifest. When <paramref name="mediaId"/>
    /// already exists, the segment is placed in that group sorted by <c>startMs</c>; otherwise a
    /// new group is appended. Preserves output-timeline stamps on existing segments.
    /// </summary>
    public static List<HighlightSourceClipGroup> AppendSegment(
        IReadOnlyList<HighlightSourceClipGroup> manifest,
        Guid mediaId,
        string sourceS3Key,
        long startMs,
        long endMs)
    {
        if (startMs < 0 || endMs <= startMs)
            throw new ArgumentException("Segment startMs/endMs are invalid.");

        return InsertSegmentsIntoManifest(
            manifest,
            mediaId,
            sourceS3Key,
            new[] { new HighlightSourceSegmentMs(startMs, endMs) });
    }

    /// <summary>
    /// Inserts raw source segments into the manifest using the same mediaId placement rules as
    /// <see cref="AppendSegment"/>.
    /// </summary>
    public static List<HighlightSourceClipGroup> AppendSourceClipGroup(
        IReadOnlyList<HighlightSourceClipGroup> manifest,
        Guid mediaId,
        string sourceS3Key,
        IReadOnlyList<HighlightSourceSegmentMs> rawSegments)
    {
        if (rawSegments.Count == 0)
            throw new ArgumentException("Raw source segments cannot be empty.");

        return InsertSegmentsIntoManifest(manifest, mediaId, sourceS3Key, rawSegments);
    }

    private static List<HighlightSourceClipGroup> InsertSegmentsIntoManifest(
        IReadOnlyList<HighlightSourceClipGroup> manifest,
        Guid mediaId,
        string sourceS3Key,
        IReadOnlyList<HighlightSourceSegmentMs> newSegments)
    {
        var result = manifest
            .Select(CloneGroupPreservingStamps)
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

        result.Add(new HighlightSourceClipGroup(
            mediaId,
            sourceS3Key,
            SortSegmentsByStartMs(newSegments)));

        return SanitizeManifest(result);
    }

    private static HighlightSourceClipGroup CloneGroupPreservingStamps(HighlightSourceClipGroup group) =>
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

    /// <summary>
    /// Drops zero-length source ranges (<c>endMs &lt;= startMs</c>) and empty clip groups.
    /// Face-timeline capture can persist degenerate segments that break stamping and add-segment.
    /// </summary>
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

    private static List<HighlightSourceSegmentMs> FilterDegenerateSegments(
        IEnumerable<HighlightSourceSegmentMs> segments) =>
        segments
            .Where(s => !s.EndMs.HasValue || s.EndMs.Value > s.StartMs)
            .ToList();

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

    /// <summary>
    /// Returns true when <paramref name="startMs"/>–<paramref name="endMs"/> on
    /// <paramref name="mediaId"/> overlaps any segment already stored in the manifest.
    /// </summary>
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

    private static bool SourceRangesOverlap(long startMs, long endMs, HighlightSourceSegmentMs existing)
    {
        if (existing.StartMs == 0 && existing.EndMs is null)
            return true;

        var existingEnd = existing.EndMs ?? existing.StartMs;
        return startMs < existingEnd && existing.StartMs < endMs;
    }

    /// <summary>
    /// Maps output-timeline exclude ranges onto the source manifest so a trim item can still
    /// be edited via <c>add-segment</c> / manifest regeneration.
    /// Requires stamped <c>outputStartMs</c>/<c>outputEndMs</c> on segments, or legacy
    /// source-duration resolution via <paramref name="fullVideoOutputDurationMsByMediaId"/>.
    /// </summary>
    public static List<HighlightSourceClipGroup> TransformForOutputTrim(
        IReadOnlyList<HighlightSourceClipGroup> manifest,
        long outputDurationMs,
        IReadOnlyList<(long StartMs, long EndMs)> excludeRanges,
        IReadOnlyDictionary<Guid, long>? fullVideoOutputDurationMsByMediaId = null)
    {
        var keepSegments = HighlightVideoTimeHelper.ComputeKeepSegments(outputDurationMs, excludeRanges);
        var blocks = BuildOutputBlocks(manifest, fullVideoOutputDurationMsByMediaId, outputDurationMs);

        var pieces = new List<SourcePiece>();
        foreach (var (keepStart, keepEnd) in keepSegments)
        {
            foreach (var block in blocks)
            {
                var intersectStart = Math.Max(keepStart, block.OutputStartMs);
                var intersectEnd = Math.Min(keepEnd, block.OutputEndMs);
                if (intersectEnd <= intersectStart)
                    continue;

                var offsetStart = intersectStart - block.OutputStartMs;
                var offsetEnd = intersectEnd - block.OutputStartMs;
                pieces.Add(new SourcePiece(
                    block.MediaId,
                    block.SourceS3Key,
                    block.SourceStartMs + offsetStart,
                    block.SourceStartMs + offsetEnd,
                    intersectStart,
                    intersectEnd));
            }
        }

        var transformed = RebuildManifestFromPieces(pieces, manifest);
        ValidateSegmentOrder(transformed);
        return transformed;
    }

    /// <summary>
    /// Stamps each segment with its span on the stitched output timeline using the actual
    /// MediaConvert output duration. Full-length source entries require
    /// <paramref name="sourceDurationMsByMediaId"/> (typically from each media asset's transcode job).
    /// </summary>
    public static List<HighlightSourceClipGroup> StampOutputTimeline(
        IReadOnlyList<HighlightSourceClipGroup> manifest,
        long actualOutputDurationMs,
        IReadOnlyDictionary<Guid, long>? sourceDurationMsByMediaId = null)
    {
        if (actualOutputDurationMs <= 0)
            throw new ArgumentException("Actual output duration must be positive.", nameof(actualOutputDurationMs));

        var sanitized = SanitizeManifest(manifest.ToList());
        var flat = FlattenSegments(sanitized);
        if (flat.Count == 0)
            return sanitized;

        var weights = flat
            .Select(entry => ResolveSegmentWeight(entry.Group.MediaId, entry.Segment, sourceDurationMsByMediaId))
            .ToList();

        var totalWeight = weights.Sum();
        if (totalWeight <= 0)
        {
            throw new InvalidOperationException("Cannot stamp output timeline: no segment weights.");
        }

        var boundaries = new long[flat.Count + 1];
        boundaries[0] = 0;
        boundaries[flat.Count] = actualOutputDurationMs;

        long allocated = 0;
        for (var i = 0; i < flat.Count - 1; i++)
        {
            var share = weights[i] * actualOutputDurationMs / totalWeight;
            allocated += share;
            boundaries[i + 1] = allocated;
        }

        var result = sanitized
            .Select(g => g with { Segments = g.Segments.ToList() })
            .ToList();

        for (var i = 0; i < flat.Count; i++)
        {
            var entry = flat[i];
            var groupIndex = result.FindIndex(g => g.MediaId == entry.Group.MediaId);
            if (groupIndex < 0)
                continue;

            var seg = entry.Segment;
            var outputStart = boundaries[i];
            var outputEnd = boundaries[i + 1];
            var endMs = ResolveStampedSourceEndMs(
                entry.Group.MediaId,
                seg,
                weights[i],
                sourceDurationMsByMediaId);

            var group = result[groupIndex];
            var segments = group.Segments.ToList();
            segments[entry.SegmentIndex] = new HighlightSourceSegmentMs(
                seg.StartMs,
                endMs,
                outputStart,
                outputEnd);
            result[groupIndex] = group with { Segments = segments };
        }

        ValidateSegmentOrder(result);
        return result;
    }

    /// <summary>
    /// Legacy fallback for manifests without stamped output spans.
    /// </summary>
    public static IReadOnlyDictionary<Guid, long> ResolveFullVideoOutputDurations(
        IReadOnlyList<HighlightSourceClipGroup> manifest,
        long parentOutputDurationMs)
    {
        var result = new Dictionary<Guid, long>();
        var unknownMediaIds = new List<Guid>();
        long knownSum = 0;

        foreach (var group in manifest)
        {
            foreach (var seg in group.Segments)
            {
                if (seg.EndMs.HasValue)
                {
                    knownSum += seg.EndMs.Value - seg.StartMs;
                    continue;
                }

                if (seg.StartMs != 0 || group.Segments.Count != 1)
                {
                    throw new InvalidOperationException(
                        $"Source clip {group.MediaId} has an ambiguous segment without endMs.");
                }

                if (!unknownMediaIds.Contains(group.MediaId))
                    unknownMediaIds.Add(group.MediaId);
            }
        }

        if (unknownMediaIds.Count == 0)
            return result;

        if (unknownMediaIds.Count == 1)
        {
            var remaining = parentOutputDurationMs - knownSum;
            if (remaining <= 0)
            {
                throw new InvalidOperationException(
                    "Parent output duration is inconsistent with the source segment manifest.");
            }

            result[unknownMediaIds[0]] = remaining;
            return result;
        }

        throw new InvalidOperationException(
            $"Cannot resolve output duration for {unknownMediaIds.Count} full-length source videos.");
    }

    public static List<ClipInput> ToClipInputs(
        IReadOnlyList<HighlightSourceClipGroup> manifest,
        int manifestVersion = RawManifestVersion)
    {
        var clipInputs = new List<ClipInput>();
        var applyMerge = manifestVersion >= RawManifestVersion;

        foreach (var group in manifest)
        {
            ValidateSegmentOrder(new[] { group });

            var isFullVideo = group.Segments.Count == 1
                              && group.Segments[0].StartMs == 0
                              && group.Segments[0].EndMs is null;

            if (isFullVideo)
            {
                clipInputs.Add(new ClipInput(group.SourceS3Key, new List<TimeClip>()));
                continue;
            }

            var timeClips = BuildTimeClipsForGroup(group.Segments, applyMerge);
            clipInputs.Add(new ClipInput(group.SourceS3Key, timeClips));
        }

        return clipInputs;
    }

    /// <summary>
    /// Stamped segments were already rendered; re-applying buffer/merge shrinks the output on regeneration.
    /// Only unstamped segments (e.g. a newly appended clip) receive merge/buffer.
    /// </summary>
    private static List<TimeClip> BuildTimeClipsForGroup(
        IReadOnlyList<HighlightSourceSegmentMs> segments,
        bool applyMergeForUnstamped)
    {
        if (segments.Count == 0)
            return new List<TimeClip>();

        if (segments.All(IsStampedSegment))
            return FormatSegmentsDirectly(segments);

        if (segments.All(s => !IsStampedSegment(s)))
        {
            return applyMergeForUnstamped
                ? HighlightVideoClipMergeHelper.MergeAndFormatToTimeClips(segments)
                : FormatSegmentsDirectly(segments);
        }

        var timeClips = new List<TimeClip>();
        foreach (var segment in segments)
        {
            if (IsStampedSegment(segment))
            {
                timeClips.AddRange(FormatSegmentsDirectly(new[] { segment }));
                continue;
            }

            timeClips.AddRange(
                applyMergeForUnstamped
                    ? HighlightVideoClipMergeHelper.MergeAndFormatToTimeClips(new[] { segment })
                    : FormatSegmentsDirectly(new[] { segment }));
        }

        return timeClips;
    }

    private static bool IsStampedSegment(HighlightSourceSegmentMs segment) =>
        segment.OutputStartMs is >= 0 && segment.OutputEndMs is > 0;

    private static List<TimeClip> FormatSegmentsDirectly(IReadOnlyList<HighlightSourceSegmentMs> segments) =>
        segments
            .Where(s => s.EndMs.HasValue)
            .Select(s => new TimeClip(
                HighlightVideoTimeHelper.MsToMediaConvertTimecode(s.StartMs),
                HighlightVideoTimeHelper.MsToMediaConvertTimecode(s.EndMs!.Value)))
            .Where(t => t.StartTimecode != t.EndTimecode)
            .ToList();

    private static List<OutputBlock> BuildOutputBlocks(
        IReadOnlyList<HighlightSourceClipGroup> manifest,
        IReadOnlyDictionary<Guid, long>? fullVideoOutputDurationMsByMediaId,
        long outputDurationMs)
    {
        if (HasStampedOutputTimeline(manifest))
        {
            var blocks = new List<OutputBlock>();
            foreach (var group in manifest)
            {
                foreach (var seg in group.Segments)
                {
                    if (seg.OutputStartMs is not long outputStart
                        || seg.OutputEndMs is not long outputEnd
                        || outputEnd <= outputStart)
                    {
                        throw new InvalidOperationException(
                            $"Source clip {group.MediaId} is missing a stamped output span.");
                    }

                    var sourceEndMs = seg.EndMs ?? seg.StartMs + (outputEnd - outputStart);
                    blocks.Add(new OutputBlock(
                        group.MediaId,
                        group.SourceS3Key,
                        seg.StartMs,
                        sourceEndMs,
                        outputStart,
                        outputEnd));
                }
            }

            return blocks;
        }

        var legacyBlocks = new List<OutputBlock>();
        var cursor = 0L;

        foreach (var group in manifest)
        {
            foreach (var seg in group.Segments)
            {
                long blockDuration;
                long sourceEndMs;

                if (seg.EndMs.HasValue)
                {
                    sourceEndMs = seg.EndMs.Value;
                    blockDuration = sourceEndMs - seg.StartMs;
                }
                else
                {
                    if (fullVideoOutputDurationMsByMediaId == null
                        || !fullVideoOutputDurationMsByMediaId.TryGetValue(group.MediaId, out blockDuration))
                    {
                        throw new InvalidOperationException(
                            $"Output duration is unknown for full-length source clip {group.MediaId}. " +
                            "Regenerate the highlight or wait for output timeline stamping to complete.");
                    }

                    sourceEndMs = seg.StartMs + blockDuration;
                }

                if (blockDuration <= 0)
                    continue;

                legacyBlocks.Add(new OutputBlock(
                    group.MediaId,
                    group.SourceS3Key,
                    seg.StartMs,
                    sourceEndMs,
                    cursor,
                    cursor + blockDuration));
                cursor += blockDuration;
            }
        }

        return legacyBlocks;
    }

    public static bool HasStampedOutputTimeline(IReadOnlyList<HighlightSourceClipGroup> manifest) =>
        manifest.SelectMany(g => g.Segments)
            .All(s => s.OutputStartMs is >= 0 && s.OutputEndMs is > 0);

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

    private static long ResolveSegmentWeight(
        Guid mediaId,
        HighlightSourceSegmentMs segment,
        IReadOnlyDictionary<Guid, long>? sourceDurationMsByMediaId)
    {
        if (segment.EndMs.HasValue)
            return segment.EndMs.Value - segment.StartMs;

        if (sourceDurationMsByMediaId != null
            && sourceDurationMsByMediaId.TryGetValue(mediaId, out var sourceDurationMs))
        {
            return Math.Max(0, sourceDurationMs - segment.StartMs);
        }

        throw new InvalidOperationException(
            $"Source duration is unknown for full-length segment on media {mediaId}.");
    }

    /// <summary>
    /// Resolves a concrete source <c>endMs</c> for stamping. Open-ended segments use source
    /// duration when available; otherwise fall back to weight with a 1ms minimum span.
    /// </summary>
    private static long ResolveStampedSourceEndMs(
        Guid mediaId,
        HighlightSourceSegmentMs segment,
        long weight,
        IReadOnlyDictionary<Guid, long>? sourceDurationMsByMediaId)
    {
        if (segment.EndMs is long closedEnd && closedEnd > segment.StartMs)
            return closedEnd;

        if (sourceDurationMsByMediaId != null
            && sourceDurationMsByMediaId.TryGetValue(mediaId, out var sourceDurationMs)
            && sourceDurationMs > segment.StartMs)
        {
            return sourceDurationMs;
        }

        var computedEnd = segment.StartMs + weight;
        return computedEnd > segment.StartMs ? computedEnd : segment.StartMs + 1;
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

    private sealed record FlatSegmentEntry(
        HighlightSourceClipGroup Group,
        int SegmentIndex,
        HighlightSourceSegmentMs Segment);

    private sealed record OutputBlock(
        Guid MediaId,
        string SourceS3Key,
        long SourceStartMs,
        long SourceEndMs,
        long OutputStartMs,
        long OutputEndMs);

    private sealed record SourcePiece(
        Guid MediaId,
        string SourceS3Key,
        long SourceStartMs,
        long SourceEndMs,
        long OutputStartMs,
        long OutputEndMs);

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
}

public sealed record HighlightClipMediaPair(
    Guid MediaId,
    DateTime MediaCreatedAt,
    ClipInput Clip,
    IReadOnlyList<HighlightSourceSegmentMs> RawSourceSegments);

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
