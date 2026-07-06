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
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public static string SerializeManifest(IReadOnlyList<HighlightSourceClipGroup> groups) =>
        JsonSerializer.Serialize(groups, JsonOpts);

    public static List<HighlightSourceClipGroup> DeserializeManifest(string json) =>
        JsonSerializer.Deserialize<List<HighlightSourceClipGroup>>(json, JsonOpts)
        ?? throw new InvalidOperationException("Failed to deserialize source segment manifest.");

    /// <summary>
    /// Builds manifest groups from generation output. <paramref name="clipsWithMedia"/> must
    /// already be ordered by <see cref="HighlightClipMediaPair.MediaCreatedAt"/> ascending.
    /// </summary>
    public static List<HighlightSourceClipGroup> BuildFromGeneration(
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
                    .OrderBy(s => s.StartMs)
                    .ToList()
                : new List<HighlightSourceSegmentMs> { new(0, null) };

            groups.Add(new HighlightSourceClipGroup(
                pair.MediaId,
                pair.Clip.S3Key,
                segments));
        }

        return groups;
    }

    /// <summary>
    /// Appends a source segment as a new clip group at the end of the manifest so the
    /// stitched highlight keeps existing content first, then plays the new segment.
    /// Clears stale output-timeline stamps because the output layout changes after re-render.
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

        var result = manifest
            .Select(g => g with
            {
                Segments = g.Segments
                    .Select(s => new HighlightSourceSegmentMs(s.StartMs, s.EndMs))
                    .ToList()
            })
            .ToList();

        result.Add(new HighlightSourceClipGroup(
            mediaId,
            sourceS3Key,
            new List<HighlightSourceSegmentMs> { new(startMs, endMs) }));

        return result;
    }

    public static void ValidateSegmentOrder(IReadOnlyList<HighlightSourceClipGroup> manifest)
    {
        foreach (var group in manifest)
        {
            if (group.Segments.Count == 0)
                throw new InvalidOperationException($"Source clip {group.MediaId} has no segments.");

            long? prevStart = null;
            foreach (var seg in group.Segments)
            {
                if (seg.StartMs < 0)
                    throw new InvalidOperationException("Segment startMs must be non-negative.");

                if (seg.EndMs.HasValue && seg.EndMs.Value <= seg.StartMs)
                    throw new InvalidOperationException("Segment endMs must be greater than startMs.");

                if (prevStart.HasValue && seg.StartMs < prevStart.Value)
                    throw new InvalidOperationException("Segments within a source clip must be ordered by startMs.");

                prevStart = seg.StartMs;
            }
        }
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

        var flat = FlattenSegments(manifest);
        if (flat.Count == 0)
            return manifest.Select(g => g with { Segments = g.Segments.ToList() }).ToList();

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

        var result = manifest
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

            long? endMs = seg.EndMs;
            if (!endMs.HasValue)
                endMs = seg.StartMs + weights[i];

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

    public static List<ClipInput> ToClipInputs(IReadOnlyList<HighlightSourceClipGroup> manifest)
    {
        var clipInputs = new List<ClipInput>();

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

            var timeClips = group.Segments
                .Where(s => s.EndMs.HasValue)
                .Select(s => new TimeClip(
                    HighlightVideoTimeHelper.MsToMediaConvertTimecode(s.StartMs),
                    HighlightVideoTimeHelper.MsToMediaConvertTimecode(s.EndMs!.Value)))
                .Where(t => t.StartTimecode != t.EndTimecode)
                .ToList();

            clipInputs.Add(new ClipInput(group.SourceS3Key, timeClips));
        }

        return clipInputs;
    }

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
    ClipInput Clip);

public sealed record HighlightSourceSegmentMs(
    long StartMs,
    long? EndMs,
    long? OutputStartMs = null,
    long? OutputEndMs = null);

public sealed record HighlightSourceClipGroup(
    Guid MediaId,
    string SourceS3Key,
    IReadOnlyList<HighlightSourceSegmentMs> Segments);
