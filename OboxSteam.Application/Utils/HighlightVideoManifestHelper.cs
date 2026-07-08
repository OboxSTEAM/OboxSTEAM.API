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

    /// <summary>
    /// Re-applies trim mapping from <paramref name="trimmedManifest"/> and appends any unstamped
    /// segments (e.g. from add-segment) from <paramref name="currentManifest"/>.
    /// </summary>
    public static List<HighlightSourceClipGroup> MergeTrimManifestWithUnstampedSegments(
        IReadOnlyList<HighlightSourceClipGroup> trimmedManifest,
        IReadOnlyList<HighlightSourceClipGroup> currentManifest)
    {
        var result = trimmedManifest
            .Select(CloneGroupPreservingStamps)
            .ToList();

        foreach (var group in currentManifest)
        {
            foreach (var segment in group.Segments.Where(s => !IsStampedOutputSegment(s)))
            {
                result = InsertSegmentsIntoManifest(result, group.MediaId, group.SourceS3Key, new[] { segment });
            }
        }

        ValidateSegmentOrder(result);
        return result;
    }

    public static bool IsStampedOutputSegment(HighlightSourceSegmentMs segment) =>
        segment.OutputStartMs is >= 0 && segment.OutputEndMs is > 0;

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
        IReadOnlyDictionary<Guid, long>? fullVideoOutputDurationMsByMediaId = null,
        int manifestVersion = RawManifestVersion,
        IReadOnlyDictionary<Guid, long>? sourceDurationMsByMediaId = null,
        bool useTrimStyleClips = false)
    {
        var keepSegments = HighlightVideoTimeHelper.ComputeKeepSegments(outputDurationMs, excludeRanges);
        var pieces = HasStampedOutputTimeline(manifest)
            ? BuildTrimPiecesFromRenderClipSpans(
                manifest, excludeRanges, outputDurationMs, manifestVersion, sourceDurationMsByMediaId, useTrimStyleClips)
            : BuildTrimPiecesFromLegacyBlocks(manifest, keepSegments, fullVideoOutputDurationMsByMediaId, outputDurationMs);

        var transformed = RebuildManifestFromPieces(pieces, manifest);
        ValidateSegmentOrder(transformed);
        return transformed;
    }

    /// <summary>
    /// Maps output keep ranges onto source using merged render-clip spans (same pipeline as encode).
    /// Raw face segments share one output span per merged clip; proportional mapping is required.
    /// </summary>
    private static List<SourcePiece> BuildTrimPiecesFromRenderClipSpans(
        IReadOnlyList<HighlightSourceClipGroup> manifest,
        IReadOnlyList<(long StartMs, long EndMs)> excludeRanges,
        long outputDurationMs,
        int manifestVersion,
        IReadOnlyDictionary<Guid, long>? sourceDurationMsByMediaId,
        bool useTrimStyleClips)
    {
        var spans = BuildRenderClipSpans(manifest, manifestVersion, useTrimStyleClips, sourceDurationMsByMediaId);
        if (spans.Count == 0)
            return new List<SourcePiece>();

        AssignOutputTimelineToRenderClipSpans(spans, manifest, outputDurationMs);
        RescaleRenderClipOutputTimeline(spans, outputDurationMs);

        var mergedExcludes = HighlightVideoTimeHelper.NormalizeMergedExcludeRanges(outputDurationMs, excludeRanges);
        var s3KeyByMediaId = manifest.ToDictionary(g => g.MediaId, g => g.SourceS3Key);
        var pieces = new List<SourcePiece>();

        foreach (var span in spans)
        {
            foreach (var (keepStart, keepEnd) in SubtractExcludesFromOutputRange(
                         span.OutputStartMs, span.OutputEndMs, mergedExcludes))
            {
                var outputSpanMs = span.OutputEndMs - span.OutputStartMs;
                var sourceSpanMs = span.SourceEndMs - span.SourceStartMs;
                if (outputSpanMs <= 0 || sourceSpanMs <= 0)
                    continue;

                var offsetStart = keepStart - span.OutputStartMs;
                var offsetEnd = keepEnd - span.OutputStartMs;
                var sourceStart = span.SourceStartMs + offsetStart * sourceSpanMs / outputSpanMs;
                var sourceEnd = span.SourceStartMs + offsetEnd * sourceSpanMs / outputSpanMs;
                if (sourceEnd <= sourceStart)
                    continue;

                pieces.Add(new SourcePiece(
                    span.MediaId,
                    s3KeyByMediaId[span.MediaId],
                    sourceStart,
                    sourceEnd,
                    keepStart,
                    keepEnd));
            }
        }

        return pieces;
    }

    /// <summary>
    /// Stretches stamped render-clip output spans so the last span ends at the actual video duration.
    /// Exclude positions from the UI are on the encoded file; stamps can drift from keyframe rounding.
    /// </summary>
    private static void RescaleRenderClipOutputTimeline(List<RenderClipSpan> spans, long outputDurationMs)
    {
        if (spans.Count == 0 || outputDurationMs <= 0)
            return;

        var maxEnd = spans.Max(s => s.OutputEndMs);
        if (maxEnd <= 0)
        {
            AllocateOutputTimeline(spans, outputDurationMs);
            return;
        }

        if (maxEnd != outputDurationMs)
        {
            for (var i = 0; i < spans.Count; i++)
            {
                var span = spans[i];
                spans[i] = span with
                {
                    OutputStartMs = span.OutputStartMs * outputDurationMs / maxEnd,
                    OutputEndMs = span.OutputEndMs * outputDurationMs / maxEnd
                };
            }
        }

        spans[0] = spans[0] with { OutputStartMs = 0 };
        spans[^1] = spans[^1] with { OutputEndMs = outputDurationMs };
    }

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

    /// <summary>
    /// Binds each render clip to its stitched-output span using stamped segment metadata.
    /// Falls back to proportional allocation when stamps are missing for a clip.
    /// </summary>
    private static void AssignOutputTimelineToRenderClipSpans(
        List<RenderClipSpan> spans,
        IReadOnlyList<HighlightSourceClipGroup> manifest,
        long outputDurationMs)
    {
        var assignedCount = 0;

        for (var i = 0; i < spans.Count; i++)
        {
            var span = spans[i];
            var group = manifest.First(g => g.MediaId == span.MediaId);
            long? outputStart = null;
            long? outputEnd = null;

            foreach (var seg in group.Segments)
            {
                var segEnd = seg.EndMs ?? seg.StartMs;
                if (segEnd <= seg.StartMs)
                    continue;

                if (!SourceRangesOverlap(seg.StartMs, segEnd, span.SourceStartMs, span.SourceEndMs))
                    continue;

                if (seg.OutputStartMs is not long os || seg.OutputEndMs is not long oe || oe <= os)
                    continue;

                outputStart = outputStart.HasValue ? Math.Min(outputStart.Value, os) : os;
                outputEnd = outputEnd.HasValue ? Math.Max(outputEnd.Value, oe) : oe;
            }

            if (outputStart is not long assignedStart || outputEnd is not long assignedEnd || assignedEnd <= assignedStart)
                continue;

            spans[i] = span with { OutputStartMs = assignedStart, OutputEndMs = assignedEnd };
            assignedCount++;
        }

        if (assignedCount < spans.Count)
            AllocateOutputTimeline(spans, outputDurationMs);
    }

    private static bool SourceRangesOverlap(long startMs, long endMs, long rangeStartMs, long rangeEndMs) =>
        startMs < rangeEndMs && rangeStartMs < endMs;

    private static List<SourcePiece> BuildTrimPiecesFromLegacyBlocks(
        IReadOnlyList<HighlightSourceClipGroup> manifest,
        IReadOnlyList<(long StartMs, long EndMs)> keepSegments,
        IReadOnlyDictionary<Guid, long>? fullVideoOutputDurationMsByMediaId,
        long outputDurationMs)
    {
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

                var blockOutputMs = block.OutputEndMs - block.OutputStartMs;
                var blockSourceMs = block.SourceEndMs - block.SourceStartMs;
                if (blockOutputMs <= 0 || blockSourceMs <= 0)
                    continue;

                var offsetStart = intersectStart - block.OutputStartMs;
                var offsetEnd = intersectEnd - block.OutputStartMs;
                var sourceStart = block.SourceStartMs + offsetStart * blockSourceMs / blockOutputMs;
                var sourceEnd = block.SourceStartMs + offsetEnd * blockSourceMs / blockOutputMs;
                if (sourceEnd <= sourceStart)
                    continue;

                pieces.Add(new SourcePiece(
                    block.MediaId,
                    block.SourceS3Key,
                    sourceStart,
                    sourceEnd,
                    intersectStart,
                    intersectEnd));
            }
        }

        return pieces;
    }

    /// <summary>
    /// Stamps each segment with its span on the stitched output timeline using the actual
    /// MediaConvert output duration. Clip spans follow the same merge/buffer rules as
    /// <see cref="ToClipInputs"/> so output stamps align with encoded segments.
    /// </summary>
    public static List<HighlightSourceClipGroup> StampOutputTimeline(
        IReadOnlyList<HighlightSourceClipGroup> manifest,
        long actualOutputDurationMs,
        IReadOnlyDictionary<Guid, long>? sourceDurationMsByMediaId = null,
        int manifestVersion = RawManifestVersion,
        bool useTrimStyleClips = false)
    {
        if (actualOutputDurationMs <= 0)
            throw new ArgumentException("Actual output duration must be positive.", nameof(actualOutputDurationMs));

        var sanitized = SanitizeManifest(manifest.ToList());
        var clipSpans = BuildRenderClipSpans(
            sanitized, manifestVersion, useTrimStyleClips, sourceDurationMsByMediaId);

        if (clipSpans.Count == 0)
            return StampOutputTimelineByRawWeight(sanitized, actualOutputDurationMs, sourceDurationMsByMediaId);

        AllocateOutputTimeline(clipSpans, actualOutputDurationMs);

        var result = sanitized
            .Select(g => g with { Segments = g.Segments.ToList() })
            .ToList();

        foreach (var group in sanitized)
        {
            var groupIndex = result.FindIndex(g => g.MediaId == group.MediaId);
            if (groupIndex < 0)
                continue;

            var updatedSegments = result[groupIndex].Segments.ToList();
            for (var i = 0; i < group.Segments.Count; i++)
            {
                var seg = group.Segments[i];
                var outputSpan = ResolveOutputSpanForSegment(
                    group.MediaId, seg, clipSpans, sourceDurationMsByMediaId);

                var endMs = ResolveStampedSourceEndMs(
                    group.MediaId,
                    seg,
                    outputSpan.SourceWeight,
                    sourceDurationMsByMediaId);

                updatedSegments[i] = new HighlightSourceSegmentMs(
                    seg.StartMs,
                    endMs,
                    outputSpan.OutputStartMs,
                    outputSpan.OutputEndMs);
            }

            result[groupIndex] = result[groupIndex] with { Segments = updatedSegments };
        }

        ValidateSegmentOrder(result);
        return result;
    }

    private static List<HighlightSourceClipGroup> StampOutputTimelineByRawWeight(
        IReadOnlyList<HighlightSourceClipGroup> manifest,
        long actualOutputDurationMs,
        IReadOnlyDictionary<Guid, long>? sourceDurationMsByMediaId)
    {
        var flat = FlattenSegments(manifest);
        if (flat.Count == 0)
            return manifest.ToList();

        var weights = flat
            .Select(entry => ResolveSegmentWeight(entry.Group.MediaId, entry.Segment, sourceDurationMsByMediaId))
            .ToList();

        var totalWeight = weights.Sum();
        if (totalWeight <= 0)
            throw new InvalidOperationException("Cannot stamp output timeline: no segment weights.");

        var boundaries = new long[flat.Count + 1];
        boundaries[0] = 0;
        boundaries[flat.Count] = actualOutputDurationMs;

        long allocated = 0;
        for (var i = 0; i < flat.Count - 1; i++)
        {
            allocated += weights[i] * actualOutputDurationMs / totalWeight;
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
                boundaries[i],
                boundaries[i + 1]);
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
        int manifestVersion = RawManifestVersion,
        bool useTrimStyleClips = false)
    {
        var clipInputs = new List<ClipInput>();
        var applyMerge = manifestVersion >= RawManifestVersion;

        foreach (var group in manifest)
        {
            ValidateSegmentOrder(new[] { group });

            if (IsFullVideoGroup(group))
            {
                clipInputs.Add(new ClipInput(group.SourceS3Key, new List<TimeClip>()));
                continue;
            }

            var timeClips = BuildTimeClipsForGroup(group.Segments, applyMerge, useTrimStyleClips);
            clipInputs.Add(new ClipInput(group.SourceS3Key, timeClips));
        }

        return clipInputs;
    }

    /// <summary>
    /// When <paramref name="useTrimStyleClips"/> is false, the whole group is merged/buffered the
    /// same way as initial generation. Trim-derived manifests keep stamped pieces direct and only
    /// merge newly appended unstamped segments.
    /// </summary>
    private static List<TimeClip> BuildTimeClipsForGroup(
        IReadOnlyList<HighlightSourceSegmentMs> segments,
        bool applyMergeForUnstamped,
        bool useTrimStyleClips = false)
    {
        if (segments.Count == 0)
            return new List<TimeClip>();

        if (!useTrimStyleClips && applyMergeForUnstamped)
            return HighlightVideoClipMergeHelper.MergeAndFormatToTimeClips(segments);

        if (!useTrimStyleClips)
            return FormatSegmentsDirectly(segments);

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

    private static bool IsFullVideoGroup(HighlightSourceClipGroup group) =>
        group.Segments.Count == 1
        && group.Segments[0].StartMs == 0
        && group.Segments[0].EndMs is null;

    private static List<RenderClipSpan> BuildRenderClipSpans(
        IReadOnlyList<HighlightSourceClipGroup> manifest,
        int manifestVersion,
        bool useTrimStyleClips,
        IReadOnlyDictionary<Guid, long>? sourceDurationMsByMediaId)
    {
        var applyMerge = manifestVersion >= RawManifestVersion;
        var spans = new List<RenderClipSpan>();

        foreach (var group in manifest)
        {
            if (IsFullVideoGroup(group))
            {
                if (sourceDurationMsByMediaId == null
                    || !sourceDurationMsByMediaId.TryGetValue(group.MediaId, out var sourceDurationMs)
                    || sourceDurationMs <= 0)
                {
                    throw new InvalidOperationException(
                        $"Source duration is unknown for full-length source clip {group.MediaId}.");
                }

                spans.Add(new RenderClipSpan(group.MediaId, 0, sourceDurationMs, 0, 0));
                continue;
            }

            var clips = BuildTimeClipsForGroup(group.Segments, applyMerge, useTrimStyleClips);
            foreach (var clip in clips)
            {
                var start = ParseMediaConvertTimecode(clip.StartTimecode);
                var end = ParseMediaConvertTimecode(clip.EndTimecode);
                if (end <= start)
                    continue;

                spans.Add(new RenderClipSpan(group.MediaId, start, end, 0, 0));
            }
        }

        return spans;
    }

    private static void AllocateOutputTimeline(List<RenderClipSpan> spans, long actualOutputDurationMs)
    {
        var totalSourceMs = spans.Sum(s => s.SourceEndMs - s.SourceStartMs);
        if (totalSourceMs <= 0)
            throw new InvalidOperationException("Cannot stamp output timeline: no render clip duration.");

        long cursor = 0;
        for (var i = 0; i < spans.Count; i++)
        {
            var sourceDuration = spans[i].SourceEndMs - spans[i].SourceStartMs;
            long outputEnd;
            if (i == spans.Count - 1)
            {
                outputEnd = actualOutputDurationMs;
            }
            else
            {
                outputEnd = cursor + sourceDuration * actualOutputDurationMs / totalSourceMs;
                if (outputEnd <= cursor)
                    outputEnd = cursor + 1;
            }

            spans[i] = spans[i] with { OutputStartMs = cursor, OutputEndMs = outputEnd };
            cursor = outputEnd;
        }
    }

    private static (long OutputStartMs, long OutputEndMs, long SourceWeight) ResolveOutputSpanForSegment(
        Guid mediaId,
        HighlightSourceSegmentMs segment,
        IReadOnlyList<RenderClipSpan> clipSpans,
        IReadOnlyDictionary<Guid, long>? sourceDurationMsByMediaId)
    {
        var segmentEndMs = segment.EndMs
            ?? sourceDurationMsByMediaId?.GetValueOrDefault(mediaId)
            ?? segment.StartMs;

        RenderClipSpan? best = null;
        long bestOverlap = 0;

        foreach (var span in clipSpans.Where(s => s.MediaId == mediaId))
        {
            var overlapStart = Math.Max(segment.StartMs, span.SourceStartMs);
            var overlapEnd = Math.Min(segmentEndMs, span.SourceEndMs);
            var overlap = overlapEnd - overlapStart;
            if (overlap > bestOverlap)
            {
                bestOverlap = overlap;
                best = span;
            }
        }

        if (best is null || bestOverlap <= 0)
        {
            throw new InvalidOperationException(
                $"Cannot map segment on media {mediaId} to a render clip span.");
        }

        var sourceWeight = Math.Max(1, segmentEndMs - segment.StartMs);
        return (best.OutputStartMs, best.OutputEndMs, sourceWeight);
    }

    private static bool IsStampedSegment(HighlightSourceSegmentMs segment) =>
        IsStampedOutputSegment(segment);

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

    private sealed record RenderClipSpan(
        Guid MediaId,
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
