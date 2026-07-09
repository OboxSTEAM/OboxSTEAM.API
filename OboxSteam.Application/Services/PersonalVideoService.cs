using Microsoft.Extensions.Logging;
using OboxSteam.Application.DTOs.MediaDTO;
using OboxSteam.Application.Interfaces;
using OboxSteam.Application.Utils;
using OboxSteam.Domain.Entities;
using OboxSteam.Domain.Enums;
using OboxSteam.Domain.Interfaces;
using System.Text.Json;

namespace OboxSteam.Application.Services;

/// <summary>
/// Orchestrates personal highlight video generation for a student within a Program.
///
/// Data source: face and label timelines are captured at tagging time and persisted
/// (<see cref="MediaTag.FaceSegmentsJson"/> / <see cref="MediaTag.HasOtherFaces"/> and
/// <see cref="MediaAsset.LabelTimelineJson"/>). This service reads them from the DB and
/// does NOT re-query Rekognition (whose video job results expire after 7 days).
///
/// Logic Core (clipping rules per source video):
///   Case 1 — Video has ZERO recorded face segments for the student (scene-only / activity
///             footage) but was tagged for the student → include the ENTIRE video without
///             strength filtering; with strength filtering use label-only Bedrock sub-clips
///             when strength filtering is enabled.
///   Case 2 — Video has no other faces besides the target student → include ENTIRE video.
///   Case 3 — Video has multiple people → extract only the student's segments from the timeline
///             using 2-second buffers; segments that overlap after buffering are merged.
///   Fallback — No persisted timeline (legacy data / capture failure): include the ENTIRE video
///              only when the student is the sole tagged person; otherwise skip the video to
///              avoid leaking other people's faces.
///
/// Strengths Filtering (optional — triggered when caller supplies a strength description):
///   Cross-references on-camera face segments with the persisted Label Detection timeline via
///   an LLM (AWS Bedrock). Only segments where visual
///   labels demonstrate the described strength are kept. Missing label timelines for a video
///   are logged and that video is skipped; the job continues with remaining clips. When no
///   segment matches across all videos the background job is marked
///   <see cref="HighlightVideoStatus.Failed"/>.
/// </summary>
public class PersonalVideoService : IPersonalVideoService
{
    private enum StrengthFilterError
    {
        None,
        /// <summary>Bedrock found no matching strength segments — skip this video.</summary>
        NoMatch,
        /// <summary>Label timeline missing or unreadable — cannot honour strength filtering.</summary>
        LabelUnavailable,
        /// <summary>Bedrock call failed and no alternate evaluation path was available.</summary>
        MatchingFailed,
    }

    private sealed record StrengthFilterResult(
        ClipInput? Clip,
        StrengthFilterError Error = StrengthFilterError.None,
        string? Detail = null);

    private sealed record MediaClipBuildResult(
        ClipInput? Clip,
        StrengthFilterError StrengthError = StrengthFilterError.None,
        string? StrengthErrorDetail = null);

    private sealed record ClipBuildResult(
        IReadOnlyList<ClipInput> Clips,
        IReadOnlyList<HighlightSourceClipGroup> RenderClips);

    // ── Clipping constants (merge/buffer lives in HighlightVideoClipMergeHelper) ──
    /// When filtering label-detection entries for Claude, include labels within this
    /// window around each face segment (before StartMs and after EndMs).
    /// 5 seconds of extra context helps Claude detect activities that start just
    /// before/after the student enters the frame.
    /// </summary>
    private const long LabelContextWindowMs = 5_000;

    private const int MaxStacksPerStudentProgram = 3;
    private const int MaxItemsPerStack = 4;

    /// <summary>
    /// A HighlightVideo stuck in <see cref="HighlightVideoStatus.Processing"/> longer than this
    /// is treated as stale (e.g. the MediaConvert completion webhook never arrived) and may be
    /// re-triggered. Without this guard a lost webhook would lock the record forever.
    /// </summary>
    private static readonly TimeSpan ProcessingStaleThreshold = TimeSpan.FromMinutes(30);

    private readonly string _s3Bucket;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IVideoConverterService _videoConverterService;
    private readonly IStrengthMatchService _strengthMatchService;
    private readonly IBlobService _blobService;
    private readonly IPersonalVideoQueue _queue;
    private readonly ILogger<PersonalVideoService> _logger;

    public PersonalVideoService(
        IUnitOfWork unitOfWork,
        IVideoConverterService videoConverterService,
        IStrengthMatchService strengthMatchService,
        IBlobService blobService,
        IPersonalVideoQueue queue,
        ILogger<PersonalVideoService> logger)
    {
        _unitOfWork = unitOfWork;
        _videoConverterService = videoConverterService;
        _strengthMatchService = strengthMatchService;
        _blobService = blobService;
        _queue = queue;
        _logger = logger;
        _s3Bucket = blobService.BucketName;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Public API
    // ─────────────────────────────────────────────────────────────────────────

    /// <inheritdoc />
    public async Task<HighlightVideoStackDto> CreateStackAsync(
        Guid programId, Guid studentId, string? strengthDescription = null)
    {
        await ValidateProgramAndStudentAsync(programId, studentId);

        var normalizedStrength = NormalizeStrengthDescription(strengthDescription);

        var existingStack = await _unitOfWork.HighlightVideoStacks.FirstOrDefaultAsync(
            s => s.ProgramId == programId
                 && s.StudentId == studentId
                 && s.StrengthDescription == normalizedStrength);

        if (existingStack != null)
        {
            var existingItems = await LoadStackItemsAsync(existingStack.Id);
            if (existingItems.Any(i => i.Status == HighlightVideoStatus.Processing))
            {
                var requestedAt = existingItems
                    .Where(i => i.Status == HighlightVideoStatus.Processing)
                    .Select(i => i.RequestedAt ?? i.CreatedAt)
                    .Max();
                if (DateTime.UtcNow - requestedAt <= ProcessingStaleThreshold)
                    return await MapStackToDtoAsync(existingStack, existingItems);
            }

            if (existingItems.Count >= MaxItemsPerStack)
                throw ErrorHelper.Conflict(
                    $"Stack already has {MaxItemsPerStack} videos. Delete an item before generating again.");

            await CreateAndEnqueueInitialItemAsync(existingStack, normalizedStrength);
            var refreshedItems = await LoadStackItemsAsync(existingStack.Id);
            return await MapStackToDtoAsync(existingStack, refreshedItems);
        }

        var stacks = await _unitOfWork.HighlightVideoStacks.GetAllAsync(
            s => s.ProgramId == programId && s.StudentId == studentId);

        if (stacks.Count >= MaxStacksPerStudentProgram)
            throw ErrorHelper.Conflict(
                $"Maximum of {MaxStacksPerStudentProgram} highlight stacks allowed per student and program. Delete an existing stack first.");

        var stack = new HighlightVideoStack
        {
            ProgramId = programId,
            StudentId = studentId,
            StrengthDescription = normalizedStrength,
        };
        await _unitOfWork.HighlightVideoStacks.AddAsync(stack);
        await _unitOfWork.SaveChangesAsync();

        await CreateAndEnqueueInitialItemAsync(stack, normalizedStrength);
        var items = await LoadStackItemsAsync(stack.Id);
        return await MapStackToDtoAsync(stack, items);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<HighlightVideoStackDto>> GetStacksAsync(Guid programId, Guid studentId)
    {
        await ValidateProgramAndStudentAsync(programId, studentId);

        var stacks = await _unitOfWork.HighlightVideoStacks.GetAllAsync(
            s => s.ProgramId == programId && s.StudentId == studentId);

        var result = new List<HighlightVideoStackDto>();
        foreach (var stack in stacks.OrderBy(s => s.CreatedAt))
        {
            var items = await LoadStackItemsAsync(stack.Id);
            result.Add(await MapStackToDtoAsync(stack, items));
        }

        return result;
    }

    /// <inheritdoc />
    public async Task<HighlightVideoStackDto?> GetStackAsync(Guid programId, Guid studentId, Guid stackId)
    {
        var stack = await _unitOfWork.HighlightVideoStacks.FirstOrDefaultAsync(
            s => s.Id == stackId && s.ProgramId == programId && s.StudentId == studentId);

        if (stack == null)
            return null;

        var items = await LoadStackItemsAsync(stack.Id);
        return await MapStackToDtoAsync(stack, items);
    }

    /// <inheritdoc />
    public async Task<HighlightVideoItemDto> TrimItemAsync(
        Guid programId,
        Guid studentId,
        Guid stackId,
        Guid parentItemId,
        TrimHighlightVideoRequest request)
    {
        await ValidateProgramAndStudentAsync(programId, studentId);

        var stack = await _unitOfWork.HighlightVideoStacks.FirstOrDefaultAsync(
            s => s.Id == stackId && s.ProgramId == programId && s.StudentId == studentId)
            ?? throw ErrorHelper.NotFound($"Highlight stack '{stackId}' not found.");

        var items = await LoadStackItemsAsync(stack.Id);
        if (items.Count >= MaxItemsPerStack)
            throw ErrorHelper.Conflict(
                $"Stack already has {MaxItemsPerStack} videos. Delete an item before trimming again.");

        if (items.Any(i => i.Status == HighlightVideoStatus.Processing))
            throw ErrorHelper.Conflict("A video in this stack is still processing.");

        var parent = items.FirstOrDefault(i => i.Id == parentItemId)
            ?? throw ErrorHelper.NotFound($"Highlight video item '{parentItemId}' not found in stack.");

        if (parent.Status != HighlightVideoStatus.Completed)
            throw ErrorHelper.BadRequest("Parent video must be completed before trimming.");

        if (parent.DurationMs is null or <= 0)
            throw ErrorHelper.BadRequest("Parent video output metadata is missing; cannot trim.");

        if (string.IsNullOrWhiteSpace(parent.SourceSegmentsJson))
            throw ErrorHelper.BadRequest(
                "Render clip manifest is not available for this item. Regenerate the highlight first.");

        if (request.ExcludeRanges.Count == 0)
            throw ErrorHelper.BadRequest("At least one exclude range is required.");

        var excludeRanges = HighlightVideoTimeHelper.ParseExcludeRanges(request.ExcludeRanges);

        foreach (var (start, end) in excludeRanges)
        {
            if (start < 0 || end > parent.DurationMs.Value || end <= start)
                throw ErrorHelper.BadRequest(
                    $"Exclude range must lie within 00:00:00 and the video duration ({parent.DurationMs}ms).");
        }

        HighlightVideoTimeHelper.ComputeKeepSegments(parent.DurationMs.Value, excludeRanges);

        string transformedManifestJson;
        try
        {
            var parsed = HighlightVideoManifestHelper.ParseManifest(parent.SourceSegmentsJson);
            var sourceDurations = await LoadSourceDurationMsByMediaIdAsync(
                parsed.Groups.Select(g => g.MediaId));
            var trimmedGroups = HighlightVideoManifestHelper.ApplyOutputTrim(
                parsed.Groups,
                parent.DurationMs.Value,
                excludeRanges,
                sourceDurations);
            // Clear output stamps — they will be re-stamped after the new encode completes.
            var cleared = trimmedGroups
                .Select(g => g with
                {
                    Segments = g.Segments
                        .Select(s => new HighlightSourceSegmentMs(s.StartMs, s.EndMs))
                        .ToList()
                })
                .ToList();
            transformedManifestJson = HighlightVideoManifestHelper.SerializeManifest(cleared);
        }
        catch (InvalidOperationException ex)
        {
            throw ErrorHelper.BadRequest($"Cannot derive render clips for trim: {ex.Message}");
        }

        var trimItem = new HighlightVideoItem
        {
            StackId = stack.Id,
            ParentItemId = parent.Id,
            GenerationKind = HighlightVideoGenerationKind.Trim,
            Status = HighlightVideoStatus.Processing,
            RequestedAt = DateTime.UtcNow,
            TrimDescription = request.TrimDescription,
            TrimExcludeRangesJson = JsonSerializer.Serialize(request.ExcludeRanges),
            SourceSegmentsJson = transformedManifestJson,
        };
        await _unitOfWork.HighlightVideoItems.AddAsync(trimItem);
        await _unitOfWork.SaveChangesAsync();

        _queue.Enqueue(new PersonalVideoJob(
            trimItem.Id,
            PersonalVideoJobKind.ManifestEncode,
            programId,
            studentId,
            StrengthDescription: null));

        return await MapItemToDtoAsync(trimItem, request.ExcludeRanges);
    }

    /// <inheritdoc />
    public async Task<HighlightVideoItemDto> AddSegmentAsync(
        Guid programId,
        Guid studentId,
        Guid stackId,
        Guid parentItemId,
        AddHighlightSegmentRequest request)
    {
        await ValidateProgramAndStudentAsync(programId, studentId);

        var stack = await _unitOfWork.HighlightVideoStacks.FirstOrDefaultAsync(
            s => s.Id == stackId && s.ProgramId == programId && s.StudentId == studentId)
            ?? throw ErrorHelper.NotFound($"Highlight stack '{stackId}' not found.");

        var items = await LoadStackItemsAsync(stack.Id);
        if (items.Count >= MaxItemsPerStack)
            throw ErrorHelper.Conflict(
                $"Stack already has {MaxItemsPerStack} videos. Delete an item before adding a segment.");

        if (items.Any(i => i.Status == HighlightVideoStatus.Processing))
            throw ErrorHelper.Conflict("A video in this stack is still processing.");

        var parent = items.FirstOrDefault(i => i.Id == parentItemId)
            ?? throw ErrorHelper.NotFound($"Highlight video item '{parentItemId}' not found in stack.");

        if (parent.Status != HighlightVideoStatus.Completed)
            throw ErrorHelper.BadRequest("Parent video must be completed before adding a segment.");

        if (string.IsNullOrWhiteSpace(parent.SourceSegmentsJson))
            throw ErrorHelper.BadRequest(
                "Source segment manifest is not available for this item. Regenerate the highlight first.");

        var startMs = HighlightVideoTimeHelper.ParseTimecodeToMs(request.Start);
        var endMs = HighlightVideoTimeHelper.ParseTimecodeToMs(request.End);
        if (startMs < 0 || endMs <= startMs)
            throw ErrorHelper.BadRequest("Segment start must be before end and non-negative.");

        var media = await ValidateSegmentMediaAsync(programId, studentId, request.MediaId);
        var s3Key = ExtractS3KeyFromUrl(media.FileUrl);
        if (string.IsNullOrEmpty(s3Key))
            throw ErrorHelper.BadRequest("Cannot resolve source video key for the selected media.");

        var sourceDurations = await LoadSourceDurationMsByMediaIdAsync(new[] { media.Id });
        if (sourceDurations.TryGetValue(media.Id, out var sourceDurationMs) && sourceDurationMs > 0)
        {
            if (startMs >= sourceDurationMs)
                throw ErrorHelper.BadRequest(
                    $"Segment start must be before the source video duration ({sourceDurationMs}ms).");
            if (endMs > sourceDurationMs)
                throw ErrorHelper.BadRequest(
                    $"Segment end must not exceed the source video duration ({sourceDurationMs}ms).");
        }

        var manifest = HighlightVideoManifestHelper.DeserializeManifest(parent.SourceSegmentsJson);

        if (HighlightVideoManifestHelper.SegmentOverlapsExisting(manifest, media.Id, startMs, endMs))
            throw ErrorHelper.Conflict(
                "This source segment overlaps a clip already included in the highlight video.");

        List<HighlightSourceClipGroup> updatedManifest;
        try
        {
            updatedManifest = HighlightVideoManifestHelper.AppendBufferedSegment(
                manifest,
                media.Id,
                s3Key,
                startMs,
                endMs);
            // Clear output stamps — re-stamped after the new encode completes.
            updatedManifest = updatedManifest
                .Select(g => g with
                {
                    Segments = g.Segments
                        .Select(s => new HighlightSourceSegmentMs(s.StartMs, s.EndMs))
                        .ToList()
                })
                .ToList();
            HighlightVideoManifestHelper.ValidateSegmentOrder(updatedManifest);
        }
        catch (ArgumentException ex)
        {
            throw ErrorHelper.BadRequest(ex.Message);
        }

        var segmentItem = new HighlightVideoItem
        {
            StackId = stack.Id,
            ParentItemId = parent.Id,
            GenerationKind = HighlightVideoGenerationKind.SegmentAdd,
            Status = HighlightVideoStatus.Processing,
            RequestedAt = DateTime.UtcNow,
            TrimDescription = request.Description,
            SourceSegmentsJson = HighlightVideoManifestHelper.SerializeManifest(updatedManifest),
        };
        await _unitOfWork.HighlightVideoItems.AddAsync(segmentItem);
        await _unitOfWork.SaveChangesAsync();

        _queue.Enqueue(new PersonalVideoJob(
            segmentItem.Id,
            PersonalVideoJobKind.ManifestEncode,
            programId,
            studentId,
            StrengthDescription: null));

        return await MapItemToDtoAsync(segmentItem);
    }

    /// <inheritdoc />
    public async Task DeleteItemAsync(Guid programId, Guid studentId, Guid stackId, Guid itemId)
    {
        await ValidateProgramAndStudentAsync(programId, studentId);

        var stack = await _unitOfWork.HighlightVideoStacks.FirstOrDefaultAsync(
            s => s.Id == stackId && s.ProgramId == programId && s.StudentId == studentId)
            ?? throw ErrorHelper.NotFound($"Highlight stack '{stackId}' not found.");

        var item = await _unitOfWork.HighlightVideoItems.FirstOrDefaultAsync(
            i => i.Id == itemId && i.StackId == stack.Id)
            ?? throw ErrorHelper.NotFound($"Highlight video item '{itemId}' not found.");

        if (item.Status == HighlightVideoStatus.Processing)
            throw ErrorHelper.Conflict("Cannot delete a video while it is processing.");

        await _unitOfWork.HighlightVideoItems.SoftRemove(item);
        await _unitOfWork.SaveChangesAsync();
    }

    /// <inheritdoc />
    public async Task DeleteStackAsync(Guid programId, Guid studentId, Guid stackId)
    {
        await ValidateProgramAndStudentAsync(programId, studentId);

        var stack = await _unitOfWork.HighlightVideoStacks.FirstOrDefaultAsync(
            s => s.Id == stackId && s.ProgramId == programId && s.StudentId == studentId)
            ?? throw ErrorHelper.NotFound($"Highlight stack '{stackId}' not found.");

        var items = await _unitOfWork.HighlightVideoItems.GetAllAsync(i => i.StackId == stack.Id);
        if (items.Any(i => i.Status == HighlightVideoStatus.Processing))
            throw ErrorHelper.Conflict("Cannot delete a stack while a video is processing.");

        foreach (var item in items)
            await _unitOfWork.HighlightVideoItems.SoftRemove(item);

        await _unitOfWork.HighlightVideoStacks.SoftRemove(stack);
        await _unitOfWork.SaveChangesAsync();
    }

    /// <inheritdoc />
    public async Task ProcessGenerationAsync(PersonalVideoJob job)
    {
        var item = await _unitOfWork.HighlightVideoItems.FirstOrDefaultAsync(
            i => i.Id == job.ItemId && !i.IsDeleted);

        if (item == null)
        {
            _logger.LogWarning(
                "[PersonalVideoService] ProcessGenerationAsync: item {Id} not found; skipping.", job.ItemId);
            return;
        }

        try
        {
            if (job.Kind == PersonalVideoJobKind.ManifestEncode)
            {
                await ProcessManifestEncodeAsync(item, job);
                return;
            }

            await ProcessInitialGenerationAsync(item, job);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "[PersonalVideoService] Generation failed for item {Id}.", item.Id);

            item.Status = HighlightVideoStatus.Failed;
            item.FailureReason =
                "An internal error occurred during video generation. Please try again later.";
            try
            {
                await _unitOfWork.SaveChangesAsync();
            }
            catch (Exception saveEx)
            {
                _logger.LogError(saveEx,
                    "[PersonalVideoService] Failed to persist Failed status for item {Id}.", item.Id);
            }
        }
    }

    /// <inheritdoc />
    public async Task HandlePersonalVideoJobCompletionAsync(string jobId, bool isSuccess)
    {
        _logger.LogInformation(
            "[PersonalVideoService] Handling MediaConvert Webhook JobId: {JobId}, Success: {Success}", jobId, isSuccess);

        var item = await _unitOfWork.HighlightVideoItems.FirstOrDefaultAsync(
            i => i.PersonalVideoJobRef == jobId && !i.IsDeleted);

        if (item == null)
            return;

        if (isSuccess)
        {
            try
            {
                var outputS3Key = await _videoConverterService.GetOutputS3KeyAsync(jobId);
                var videoUrl = await _blobService.GetPreviewUrlAsync(outputS3Key);
                var durationMs = await _videoConverterService.GetOutputDurationMsAsync(jobId);

                item.OutputS3Key = outputS3Key;
                item.VideoUrl = videoUrl;
                item.DurationMs = durationMs ?? item.DurationMs;
                item.Status = HighlightVideoStatus.Completed;

                if (!string.IsNullOrWhiteSpace(item.SourceSegmentsJson) && item.DurationMs is > 0)
                {
                    try
                    {
                        var parsed = HighlightVideoManifestHelper.ParseManifest(item.SourceSegmentsJson);
                        var mediaIds = parsed.Groups.Select(g => g.MediaId);
                        var sourceDurations = await LoadSourceDurationMsByMediaIdAsync(mediaIds);
                        var stamped = HighlightVideoManifestHelper.StampOutputTimeline(
                            parsed.Groups,
                            item.DurationMs.Value,
                            sourceDurations);
                        item.SourceSegmentsJson = HighlightVideoManifestHelper.SerializeManifest(stamped);
                    }
                    catch (Exception stampEx)
                    {
                        _logger.LogWarning(stampEx,
                            "[PersonalVideoService] Failed to stamp output timeline for item {Id}.", item.Id);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "[PersonalVideoService] Failed to resolve MediaConvert output for item {Id}", item.Id);
                item.Status = HighlightVideoStatus.Failed;
                item.FailureReason = "Failed to resolve transcoded output.";
            }
        }
        else
        {
            item.Status = HighlightVideoStatus.Failed;
            item.FailureReason = "MediaConvert job failed.";
        }

        await _unitOfWork.SaveChangesAsync();
    }

    private async Task ValidateProgramAndStudentAsync(Guid programId, Guid studentId)
    {
        var program = await _unitOfWork.Programs.GetByIdAsync(programId);
        if (program == null || program.IsDeleted)
            throw ErrorHelper.NotFound($"Program '{programId}' not found.");

        var student = await _unitOfWork.Users.GetByIdAsync(studentId);
        if (student == null || student.IsDeleted)
            throw ErrorHelper.NotFound($"Student '{studentId}' not found.");
    }

    private static string NormalizeStrengthDescription(string? strengthDescription) =>
        string.IsNullOrWhiteSpace(strengthDescription)
            ? string.Empty
            : strengthDescription.Trim();

    private async Task<List<HighlightVideoItem>> LoadStackItemsAsync(Guid stackId)
    {
        var items = await _unitOfWork.HighlightVideoItems.GetAllAsync(i => i.StackId == stackId);
        return items.OrderBy(i => i.CreatedAt).ToList();
    }

    private async Task<HighlightVideoItem> CreateAndEnqueueInitialItemAsync(
        HighlightVideoStack stack,
        string normalizedStrength)
    {
        var item = new HighlightVideoItem
        {
            StackId = stack.Id,
            GenerationKind = HighlightVideoGenerationKind.Initial,
            Status = HighlightVideoStatus.Processing,
            RequestedAt = DateTime.UtcNow,
        };
        await _unitOfWork.HighlightVideoItems.AddAsync(item);
        await _unitOfWork.SaveChangesAsync();

        var strengthForJob = string.IsNullOrEmpty(normalizedStrength) ? null : normalizedStrength;
        _queue.Enqueue(new PersonalVideoJob(
            item.Id,
            PersonalVideoJobKind.InitialGeneration,
            stack.ProgramId,
            stack.StudentId,
            strengthForJob));

        _logger.LogInformation(
            "[PersonalVideoService] Initial generation queued. StackId={StackId}, ItemId={ItemId}",
            stack.Id, item.Id);

        return item;
    }

    private async Task ProcessInitialGenerationAsync(HighlightVideoItem item, PersonalVideoJob job)
    {
        var buildResult = await BuildClipInputsAsync(job.ProgramId, job.StudentId, job.StrengthDescription);

        var clipInputs = buildResult.Clips;
        if (clipInputs.Count == 0)
        {
            var reason = !string.IsNullOrWhiteSpace(job.StrengthDescription)
                ? $"No video segments matched the specified strengths: '{job.StrengthDescription}'. " +
                  "Ensure the student's strengths are visible in the tagged videos."
                : "No processed video assets tagged for this student were found in the program.";

            item.Status = HighlightVideoStatus.Failed;
            item.FailureReason = reason;
            await _unitOfWork.SaveChangesAsync();
            return;
        }

        item.SourceSegmentsJson = HighlightVideoManifestHelper.SerializeManifest(buildResult.RenderClips);

        var outputKey = BuildOutputS3Key(job.StudentId, item.Id);
        var mcJobId = await _videoConverterService.SubmitPersonalVideoJobAsync(clipInputs.ToList(), outputKey);

        item.PersonalVideoJobRef = mcJobId;
        item.Status = HighlightVideoStatus.Processing;
        item.FailureReason = null;
        await _unitOfWork.SaveChangesAsync();
    }

    private async Task ProcessManifestEncodeAsync(HighlightVideoItem item, PersonalVideoJob job)
    {
        if (string.IsNullOrWhiteSpace(item.SourceSegmentsJson))
        {
            item.Status = HighlightVideoStatus.Failed;
            item.FailureReason = "Render clip manifest is missing.";
            await _unitOfWork.SaveChangesAsync();
            return;
        }

        ParsedHighlightManifest parsed;
        try
        {
            parsed = HighlightVideoManifestHelper.ParseManifest(item.SourceSegmentsJson);
            HighlightVideoManifestHelper.ValidateSegmentOrder(parsed.Groups);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "[PersonalVideoService] Invalid render manifest for item {Id}.", item.Id);
            item.Status = HighlightVideoStatus.Failed;
            item.FailureReason = "Render clip manifest is invalid.";
            await _unitOfWork.SaveChangesAsync();
            return;
        }

        // Persist upgraded v3 form when reading legacy manifests.
        item.SourceSegmentsJson = HighlightVideoManifestHelper.SerializeManifest(parsed.Groups);

        var clipInputs = HighlightVideoManifestHelper.ToClipInputs(parsed.Groups);
        if (clipInputs.Count == 0)
        {
            item.Status = HighlightVideoStatus.Failed;
            item.FailureReason = "Render clip manifest produced no clips.";
            await _unitOfWork.SaveChangesAsync();
            return;
        }

        var outputKey = BuildOutputS3Key(job.StudentId, item.Id);
        var mcJobId = await _videoConverterService.SubmitPersonalVideoJobAsync(clipInputs, outputKey);

        item.PersonalVideoJobRef = mcJobId;
        item.Status = HighlightVideoStatus.Processing;
        item.FailureReason = null;
        await _unitOfWork.SaveChangesAsync();
    }

    private static string BuildOutputS3Key(Guid studentId, Guid itemId) =>
        $"{HighlightVideoConstants.OutputFolder}/{studentId}_{itemId}_{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}.mp4";

    // ─────────────────────────────────────────────────────────────────────────
    // Logic Core
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Traverses Program → Module → Course → Activity → MediaAsset to collect all
    /// <c>TaggingComplete</c> video assets that have a <see cref="MediaTag"/> for
    /// <paramref name="studentId"/>, then applies the Logic Core rules to build the
    /// ordered list of <see cref="ClipInput"/> objects for the MediaConvert job.
    /// When <paramref name="strengthDescription"/> is provided, each video's face segments are
    /// filtered via Bedrock against the label detection timeline. Missing label data
    /// filtered via Bedrock against the label detection timeline. Missing label data for a video
    /// causes that video to be skipped while the build continues with available clips.
    /// </summary>
    private async Task<ClipBuildResult> BuildClipInputsAsync(
        Guid programId, Guid studentId, string? strengthDescription = null)
    {
        _logger.LogInformation(
            "[PersonalVideoService] BuildClipInputsAsync: ProgramId={ProgramId}, StudentId={StudentId}",
            programId, studentId);

        // Traverse: Program → Modules → Courses → Activities
        var modules = await _unitOfWork.Modules.GetAllAsync(
            m => m.ProgramId == programId && !m.IsDeleted);

        var moduleIds = modules.Select(m => m.Id).ToList();

        var courses = await _unitOfWork.Courses.GetAllAsync(
            c => moduleIds.Contains(c.ModuleId) && !c.IsDeleted);

        var courseIds = courses.Select(c => c.Id).ToList();

        var activities = await _unitOfWork.Activities.GetAllAsync(
            a => courseIds.Contains(a.CourseId) && !a.IsDeleted);

        var activityIds = activities.Select(a => a.Id).ToList();

        // All tagged-complete videos for this student in the program
        var allMedia = await _unitOfWork.MediaAssets.GetAllAsync(
            m => activityIds.Contains(m.ActivityId!.Value)
                 && !m.IsDeleted
                 && m.FileType == "video"
                 && m.VideoStatus == VideoProcessingStatus.TaggingComplete,
            m => m.MediaTags);

        var taggedMedia = allMedia
            .Where(m => m.MediaTags.Any(t => t.StudentId == studentId))
            .OrderBy(m => m.CreatedAt)
            .ToList();

        _logger.LogInformation(
            "[PersonalVideoService] Found {Count} tagged video(s) for StudentId={StudentId}",
            taggedMedia.Count, studentId);

        // Build ClipInputs SEQUENTIALLY when strengths filtering is enabled.
        // Running Bedrock calls in parallel on 3+ videos blows the tokens-per-minute quota
        // (several thousand labels per video → ~100k+ tokens in one burst). Sequential
        // processing means only one LLM call is in-flight at a time, keeping token
        // consumption well within limits.
        // Both face and label timelines are read from the DB here (captured at tagging time),
        // so there are NO Rekognition calls in this loop — the no-strengths path is pure
        // DB reads and is effectively instant.
        var clips = new List<ClipInput>();
        var clipsWithMedia = new List<HighlightClipMediaPair>();
        var strengthPrerequisiteErrors = new List<string>();
        var strengthFilteringEnabled = !string.IsNullOrWhiteSpace(strengthDescription);

        foreach (var media in taggedMedia)
        {
            var s3Key = ExtractS3KeyFromUrl(media.FileUrl);
            if (string.IsNullOrEmpty(s3Key))
            {
                _logger.LogWarning(
                    "[PersonalVideoService] Cannot extract S3 key from FileUrl for MediaId={MediaId}. Skipping.",
                    media.Id);
                continue;
            }

            var result = await BuildClipInputForMediaAsync(media, s3Key, studentId, strengthDescription);
            if (result.StrengthError is StrengthFilterError.LabelUnavailable or StrengthFilterError.MatchingFailed)
            {
                strengthPrerequisiteErrors.Add(result.StrengthErrorDetail ?? $"MediaId={media.Id}");
                continue;
            }

            if (result.Clip != null)
            {
                clips.Add(result.Clip);
                clipsWithMedia.Add(new HighlightClipMediaPair(
                    media.Id,
                    media.CreatedAt,
                    result.Clip));
            }
        }

        if (strengthFilteringEnabled && strengthPrerequisiteErrors.Count > 0)
        {
            _logger.LogWarning(
                "[PersonalVideoService] Strength prerequisites missing for some videos; continuing with available clips: {Details}",
                string.Join("; ", strengthPrerequisiteErrors));
        }

        _logger.LogInformation(
            "[PersonalVideoService] BuildClipInputsAsync completed: {Count} ClipInput(s) built.",
            clips.Count);

        var renderClips = HighlightVideoManifestHelper.BuildFromRenderClipInputs(clipsWithMedia);
        return new ClipBuildResult(clips, renderClips);
    }

    /// <summary>
    /// Applies Logic Core rules to a single <see cref="MediaAsset"/> and returns the
    /// corresponding <see cref="ClipInput"/> (with or without <see cref="TimeClip"/>s).
    /// When <paramref name="strengthDescription"/> is provided, on-camera face segments are
    /// cross-referenced against the Label Detection timeline via Bedrock before being used as clips.
    /// Returns a skipped video (<c>Clip</c> null, no error) when strengths filtering yields no
    /// matched segments. Returns <see cref="StrengthFilterError.LabelUnavailable"/> or
    /// <see cref="StrengthFilterError.MatchingFailed"/> when strength filtering cannot run.
    /// </summary>
    private async Task<MediaClipBuildResult> BuildClipInputForMediaAsync(
        MediaAsset media, string s3Key, Guid studentId, string? strengthDescription = null)
    {
        _logger.LogInformation(
            "[PersonalVideoService] BuildClipInputForMediaAsync entry: MediaId={MediaId}, StudentId={StudentId}, " +
            "S3Key={S3Key}, HasStrength={HasStrength}",
            media.Id, studentId, s3Key, !string.IsNullOrWhiteSpace(strengthDescription));

        var timelineResult = ReadPersistedTimeline(media, studentId);

        if (timelineResult == null)
        {
            // No persisted timeline: legacy media tagged before timelines were captured,
            // or capture failed. Apply a privacy-safe fallback — only include the whole
            // video when the student is the ONLY tagged person; otherwise skip the video
            // to avoid leaking other people's faces into a personal highlight.
            var taggedStudentCount = media.MediaTags
                .Select(t => t.StudentId)
                .Distinct()
                .Count();

            if (taggedStudentCount > 1)
            {
                _logger.LogWarning(
                    "[PersonalVideoService] No persisted timeline and {Count} tagged students → skipping video (privacy-safe). MediaId={MediaId}",
                    taggedStudentCount, media.Id);
                return new MediaClipBuildResult(null);
            }

            _logger.LogWarning(
                "[PersonalVideoService] No persisted timeline; sole tagged student → full video. MediaId={MediaId}",
                media.Id);
            return new MediaClipBuildResult(new ClipInput(s3Key, new List<TimeClip>()));
        }

        // ── Case 1: No student face detected (AI fallback / scene-only) ─────────
        if (timelineResult.Segments.Count == 0)
        {
            _logger.LogInformation(
                "[PersonalVideoService] Case 1 selected: zero face segments, HasOtherFaces={HasOtherFaces}. MediaId={MediaId}",
                timelineResult.HasOtherFaces, media.Id);

            if (!string.IsNullOrWhiteSpace(strengthDescription))
                return await ResolveStrengthFilterOrSkipAsync(
                    media, s3Key, timelineResult.Segments, strengthDescription, "Case 1");

            _logger.LogInformation(
                "[PersonalVideoService] Case 1 (no student face detected by AI) → full video. MediaId={MediaId}", media.Id);

            return new MediaClipBuildResult(new ClipInput(s3Key, new List<TimeClip>()));
        }

        // ── Case 2: Only this student's face in the video ────────────
        if (!timelineResult.HasOtherFaces)
        {
            _logger.LogInformation(
                "[PersonalVideoService] Case 2 (sole face per persisted timeline). MediaId={MediaId}", media.Id);

            // Even when the student is the only person, apply strengths filter if requested —
            // they may only demonstrate the strength for part of the video.
            if (!string.IsNullOrWhiteSpace(strengthDescription))
                return await ResolveStrengthFilterOrSkipAsync(
                    media, s3Key, timelineResult.Segments, strengthDescription, "Case 2");

            return new MediaClipBuildResult(new ClipInput(s3Key, new List<TimeClip>()));
        }

        // ── Case 3 & 4: Multiple people — extract student's timeline ──────────────
        _logger.LogInformation(
            "[PersonalVideoService] Case 3 & 4 (mixed faces per persisted timeline) → extracting timeline. MediaId={MediaId}", media.Id);

        var faceSegments = timelineResult.Segments;

        // ── Strengths Filtering (optional) ────────────────────────────────────────
        if (!string.IsNullOrWhiteSpace(strengthDescription))
            return await ResolveStrengthFilterOrSkipAsync(
                media, s3Key, faceSegments, strengthDescription, "Case 3/4");

        // Standard face-timeline path (no strengths).
        var timeClips = MergeAndFormatTimeClips(
            faceSegments.Select(s => new MatchedSegment(s.StartMs, s.EndMs, "", 0, Array.Empty<string>())));

        _logger.LogInformation(
            "[PersonalVideoService] Case 3/4: {Face} face segment(s) → {Merged} merged clip(s) for MediaId={MediaId}. Clips=[{Clips}]",
            faceSegments.Count, timeClips.Count, media.Id,
            string.Join(", ", timeClips.Select(c => $"{c.StartTimecode}→{c.EndTimecode}")));

        return new MediaClipBuildResult(new ClipInput(s3Key, timeClips));
    }

    /// <summary>
    /// Reconstructs the student's face timeline from the <see cref="MediaTag"/> captured at
    /// tagging time (<see cref="MediaTag.FaceSegmentsJson"/> + <see cref="MediaTag.HasOtherFaces"/>).
    /// Returns <c>null</c> when no timeline was persisted for this student (legacy data created
    /// before this field existed, or a capture failure) so the caller can apply a fallback policy.
    /// </summary>
    private VideoFaceTimelineResult? ReadPersistedTimeline(MediaAsset media, Guid studentId)
    {
        var tag = media.MediaTags.FirstOrDefault(t => t.StudentId == studentId);
        if (tag?.FaceSegmentsJson == null)
            return null;

        try
        {
            var segments = JsonSerializer.Deserialize<List<FaceTimestampSegment>>(tag.FaceSegmentsJson)
                           ?? new List<FaceTimestampSegment>();
            return new VideoFaceTimelineResult(tag.HasOtherFaces, segments);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "[PersonalVideoService] Failed to deserialize FaceSegmentsJson for MediaId={MediaId}, StudentId={StudentId}.",
                media.Id, studentId);
            return null;
        }
    }

    /// <summary>
    /// When strength filtering is active, returns matched sub-clips on success or skips the
    /// video on no match — never falls back to full-video / face-only clipping.
    /// </summary>
    private async Task<MediaClipBuildResult> ResolveStrengthFilterOrSkipAsync(
        MediaAsset media,
        string s3Key,
        IList<FaceTimestampSegment> faceSegments,
        string strengthDescription,
        string caseLabel)
    {
        var filteredResult = await ApplyStrengthsFilterAsync(
            media, s3Key, faceSegments, strengthDescription);

        if (filteredResult.Error is StrengthFilterError.LabelUnavailable or StrengthFilterError.MatchingFailed)
        {
            return new MediaClipBuildResult(
                null,
                StrengthError: filteredResult.Error,
                StrengthErrorDetail: filteredResult.Detail);
        }

        if (filteredResult.Error == StrengthFilterError.None
            && filteredResult.Clip != null
            && filteredResult.Clip.Clips.Count > 0)
        {
            _logger.LogInformation(
                "[PersonalVideoService] {Case} + strengths: {Count} sub-clip(s). MediaId={MediaId}",
                caseLabel, filteredResult.Clip.Clips.Count, media.Id);
            return new MediaClipBuildResult(filteredResult.Clip);
        }

        _logger.LogInformation(
            "[PersonalVideoService] {Case} + strengths: no LLM match → skipping video. MediaId={MediaId}",
            caseLabel, media.Id);
        return new MediaClipBuildResult(null);
    }

    /// <summary>
    /// Loads the Label Detection timeline for <paramref name="media"/> then calls
    /// <see cref="IStrengthMatchService.MatchStrengthsAsync"/> for on-camera face windows, or
    /// <see cref="IStrengthMatchService.MatchStrengthsFromLabelsOnlyAsync"/> when there are no
    /// face segments (Case 1).
    /// </summary>
    /// <returns>
    /// <see cref="StrengthFilterError.None"/> with a populated <c>Clip</c> on success;
    /// <see cref="StrengthFilterError.NoMatch"/> with an empty clip when nothing matched;
    /// <see cref="StrengthFilterError.LabelUnavailable"/> or <see cref="StrengthFilterError.MatchingFailed"/>
    /// when strength filtering cannot be honoured.
    /// </returns>
    private async Task<StrengthFilterResult> ApplyStrengthsFilterAsync(
        MediaAsset media, string s3Key,
        IList<FaceTimestampSegment> faceSegments,
        string strengthDescription)
    {
        // Read the label timeline captured at label-detection time (no Rekognition re-query —
        // its results expire after 7 days). See MediaAsset.LabelTimelineJson.
        if (string.IsNullOrEmpty(media.LabelTimelineJson))
        {
            var detail =
                $"MediaId={media.Id}: label detection timeline is missing (job may still be running or failed).";
            _logger.LogWarning(
                "[PersonalVideoService] {Detail}", detail);
            return new StrengthFilterResult(null, Error: StrengthFilterError.LabelUnavailable, Detail: detail);
        }

        List<LabelDetectionEntry>? labelTimeline;
        try
        {
            labelTimeline = JsonSerializer.Deserialize<List<LabelDetectionEntry>>(media.LabelTimelineJson);
        }
        catch (Exception ex)
        {
            var detail = $"MediaId={media.Id}: failed to read label detection timeline.";
            _logger.LogWarning(ex,
                "[PersonalVideoService] Failed to deserialize LabelTimelineJson for MediaId={MediaId}.",
                media.Id);
            return new StrengthFilterResult(null, Error: StrengthFilterError.LabelUnavailable, Detail: detail);
        }

        if (labelTimeline == null || labelTimeline.Count == 0)
        {
            var detail = $"MediaId={media.Id}: label detection timeline is empty.";
            _logger.LogWarning(
                "[PersonalVideoService] Persisted label timeline is empty for MediaId={MediaId}.",
                media.Id);
            return new StrengthFilterResult(
                new ClipInput(s3Key, new List<TimeClip>()),
                Error: StrengthFilterError.NoMatch,
                Detail: detail);
        }

        // ── Token optimisation ────────────────────────────────────────────────
        // Scene-only (Case 1): no face windows — scan full label timeline via label-only Bedrock.
        // Otherwise send labels within face windows only.
        var isSceneOnly = faceSegments.Count == 0;
        var faceRelevantLabels = isSceneOnly
            ? new List<LabelDetectionEntry>()
            : FilterLabelsToSegmentWindows(labelTimeline, faceSegments);

        _logger.LogInformation(
            "[PersonalVideoService] ApplyStrengthsFilter path: MediaId={MediaId}, sceneOnly={SceneOnly}, " +
            "faceSegments={FaceCount}, totalLabels={TotalLabels}, faceLabels={FaceLabels}",
            media.Id, isSceneOnly, faceSegments.Count, labelTimeline.Count, faceRelevantLabels.Count);

        if (!isSceneOnly && faceRelevantLabels.Count == 0)
        {
            _logger.LogInformation(
                "[PersonalVideoService] No labels within face windows for MediaId={MediaId}. Skipping video.",
                media.Id);
            return new StrengthFilterResult(
                new ClipInput(s3Key, new List<TimeClip>()),
                Error: StrengthFilterError.NoMatch);
        }

        _logger.LogInformation(
            "[PersonalVideoService] Label timeline filtered for MediaId={MediaId}: sceneOnly={SceneOnly}, face {FaceLabels} label(s).",
            media.Id, isSceneOnly, faceRelevantLabels.Count);

        var allMatched = new List<MatchedSegment>();
        string reasoning = "";

        // ── Scene-only: full label timeline (no face constraint) ──────────────
        if (isSceneOnly)
        {
            try
            {
                var labelOnlyMatch = await _strengthMatchService.MatchStrengthsFromLabelsOnlyAsync(
                    labelTimeline, strengthDescription);
                allMatched.AddRange(labelOnlyMatch.MatchedSegments);
                reasoning = labelOnlyMatch.Reasoning;
            }
            catch (Exception ex)
            {
                var detail = $"MediaId={media.Id}: label-only strength matching failed (scene-only / Case 1).";
                _logger.LogWarning(ex,
                    "[PersonalVideoService] Label-only strength matching failed for MediaId={MediaId}.",
                    media.Id);
                return new StrengthFilterResult(null, Error: StrengthFilterError.MatchingFailed, Detail: detail);
            }
        }

        // ── On-camera: face segments + labels in face windows ─────────────────
        if (!isSceneOnly && faceRelevantLabels.Count > 0)
        {
            try
            {
                var faceMatch = await _strengthMatchService.MatchStrengthsAsync(
                    faceSegments, faceRelevantLabels, strengthDescription);
                allMatched.AddRange(faceMatch.MatchedSegments);
                reasoning = faceMatch.Reasoning;
            }
            catch (Exception ex)
            {
                var detail = $"MediaId={media.Id}: strength matching failed for on-camera segments.";
                _logger.LogWarning(ex,
                    "[PersonalVideoService] Claude strength matching (face) failed for MediaId={MediaId}.",
                    media.Id);
                return new StrengthFilterResult(null, Error: StrengthFilterError.MatchingFailed, Detail: detail);
            }
        }

        _logger.LogInformation(
            "[PersonalVideoService] ApplyStrengthsFilter result: MediaId={MediaId}, totalMatched={TotalMatched}",
            media.Id, allMatched.Count);

        if (allMatched.Count == 0)
        {
            _logger.LogInformation(
                "[PersonalVideoService] No strength matches for MediaId={MediaId}. Reasoning: {Reasoning}",
                media.Id, reasoning);
            return new StrengthFilterResult(
                new ClipInput(s3Key, new List<TimeClip>()),
                Error: StrengthFilterError.NoMatch);
        }

        var timeClips = MergeAndFormatTimeClips(allMatched);

        _logger.LogInformation(
            "[PersonalVideoService] Strengths filter: {Count} clip(s) for MediaId={MediaId}. Clips=[{Clips}]. Reasoning: {Reasoning}",
            timeClips.Count, media.Id,
            string.Join(", ", timeClips.Select(c => $"{c.StartTimecode}→{c.EndTimecode}")),
            reasoning);

        return new StrengthFilterResult(new ClipInput(s3Key, timeClips));
    }

    private static List<TimeClip> MergeAndFormatTimeClips(IEnumerable<MatchedSegment> segments) =>
        HighlightVideoClipMergeHelper.MergeAndFormatToTimeClips(
            segments.Select(s => (s.StartMs, s.EndMs)));

    // ─────────────────────────────────────────────────────────────────────────
    // Segment processing helpers
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Filters <paramref name="allLabels"/> to only those entries whose timestamp
    /// falls within any segment ± <see cref="LabelContextWindowMs"/>.
    /// This reduces the prompt token count on long videos while preserving contextually
    /// relevant label data for Bedrock to reason about.
    /// </summary>
    private static List<LabelDetectionEntry> FilterLabelsToSegmentWindows(
        IList<LabelDetectionEntry> allLabels,
        IList<FaceTimestampSegment> segments)
    {
        var windows = segments
            .Select(s => (Start: s.StartMs - LabelContextWindowMs, End: s.EndMs + LabelContextWindowMs))
            .OrderBy(w => w.Start)
            .ToList();

        return allLabels
            .Where(label => windows.Any(w => label.TimestampMs >= w.Start && label.TimestampMs <= w.End))
            .ToList();
    }

    /// <summary>
    /// Extracts the S3 bucket-relative key from a presigned or public URL.
    /// Falls back to treating the raw FileUrl as a key if parsing fails.
    /// </summary>
    private string? ExtractS3KeyFromUrl(string? fileUrl)
    {
        if (string.IsNullOrWhiteSpace(fileUrl)) return null;

        try
        {
            var uri = new Uri(fileUrl);
            // Presigned URLs: path = /{bucket}/{key}  OR  /{key} depending on style
            // We strip the leading '/' and any bucket prefix.
            var path = uri.AbsolutePath.TrimStart('/');

            // If the path starts with the bucket name, strip it
            if (path.StartsWith(_s3Bucket + "/", StringComparison.OrdinalIgnoreCase))
                path = path[(_s3Bucket.Length + 1)..];

            return path;
        }
        catch
        {
            // Treat as raw key
            return fileUrl;
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // DTO mapping
    // ─────────────────────────────────────────────────────────────────────────

    private async Task<MediaAsset> ValidateSegmentMediaAsync(Guid programId, Guid studentId, Guid mediaId)
    {
        var media = await _unitOfWork.MediaAssets.GetByIdAsync(mediaId, m => m.MediaTags);
        if (media == null || media.IsDeleted || media.FileType != "video")
            throw ErrorHelper.NotFound($"Media '{mediaId}' not found.");

        if (media.VideoStatus != VideoProcessingStatus.TaggingComplete)
            throw ErrorHelper.BadRequest("Source video must finish tagging before it can be added.");

        if (!media.MediaTags.Any(t => !t.IsDeleted && t.StudentId == studentId))
            throw ErrorHelper.BadRequest("The selected source video is not tagged for this student.");

        if (media.ActivityId is null)
            throw ErrorHelper.BadRequest("Source video is not linked to an activity.");

        var modules = await _unitOfWork.Modules.GetAllAsync(m => m.ProgramId == programId && !m.IsDeleted);
        var moduleIds = modules.Select(m => m.Id).ToList();
        var courses = await _unitOfWork.Courses.GetAllAsync(c => moduleIds.Contains(c.ModuleId) && !c.IsDeleted);
        var courseIds = courses.Select(c => c.Id).ToList();
        var activities = await _unitOfWork.Activities.GetAllAsync(
            a => courseIds.Contains(a.CourseId) && !a.IsDeleted);
        var activityIds = activities.Select(a => a.Id).ToHashSet();

        if (!activityIds.Contains(media.ActivityId.Value))
            throw ErrorHelper.BadRequest("Source video does not belong to this program.");

        return media;
    }

    private async Task<Dictionary<Guid, long>> LoadSourceDurationMsByMediaIdAsync(IEnumerable<Guid> mediaIds)
    {
        var ids = mediaIds.Distinct().ToList();
        var result = new Dictionary<Guid, long>();
        if (ids.Count == 0)
            return result;

        var assets = await _unitOfWork.MediaAssets.GetAllAsync(m => ids.Contains(m.Id));
        foreach (var asset in assets)
        {
            if (string.IsNullOrWhiteSpace(asset.MediaConvertJobId))
                continue;

            var durationMs = await _videoConverterService.GetOutputDurationMsAsync(asset.MediaConvertJobId);
            if (durationMs is > 0)
                result[asset.Id] = durationMs.Value;
        }

        return result;
    }

    private async Task<HighlightVideoStackDto> MapStackToDtoAsync(
        HighlightVideoStack stack,
        IReadOnlyList<HighlightVideoItem> items)
    {
        var hasProcessingItem = items.Any(i => i.Status == HighlightVideoStatus.Processing);
        var remainingSlots = Math.Max(0, MaxItemsPerStack - items.Count);

        var mappedItems = new List<HighlightVideoItemDto>();
        foreach (var item in items)
            mappedItems.Add(await MapItemToDtoAsync(item));

        return new HighlightVideoStackDto
        {
            Id = stack.Id,
            ProgramId = stack.ProgramId,
            StudentId = stack.StudentId,
            StrengthDescription = string.IsNullOrEmpty(stack.StrengthDescription)
                ? null
                : stack.StrengthDescription,
            CreatedAt = stack.CreatedAt,
            ItemCount = items.Count,
            MaxItems = MaxItemsPerStack,
            HasProcessingItem = hasProcessingItem,
            CanCreateItem = remainingSlots > 0 && !hasProcessingItem,
            Items = mappedItems,
        };
    }

    private async Task<HighlightVideoItemDto> MapItemToDtoAsync(
        HighlightVideoItem item,
        IReadOnlyList<TimeRangeDto>? excludeRanges = null)
    {
        IReadOnlyList<TimeRangeDto>? ranges = excludeRanges;
        if (ranges == null && !string.IsNullOrWhiteSpace(item.TrimExcludeRangesJson))
        {
            ranges = JsonSerializer.Deserialize<List<TimeRangeDto>>(item.TrimExcludeRangesJson)
                     ?? new List<TimeRangeDto>();
        }

        var sourceClips = await MapSourceClipsToDtoAsync(item.SourceSegmentsJson);

        return new HighlightVideoItemDto
        {
            Id = item.Id,
            StackId = item.StackId,
            ParentItemId = item.ParentItemId,
            GenerationKind = item.GenerationKind,
            VideoUrl = item.VideoUrl,
            DurationMs = item.DurationMs,
            Status = item.Status,
            StatusLabel = GetHighlightStatusLabel(item.Status),
            RequestedAt = item.RequestedAt,
            FailureReason = item.FailureReason,
            TrimDescription = item.TrimDescription,
            TrimExcludeRanges = ranges,
            SourceClips = sourceClips,
        };
    }

    private static string GetHighlightStatusLabel(HighlightVideoStatus status) => status switch
    {
        HighlightVideoStatus.None => "None",
        HighlightVideoStatus.Processing => "Processing",
        HighlightVideoStatus.Completed => "Ready",
        HighlightVideoStatus.Failed => "Failed",
        _ => status.ToString()
    };

    private async Task<IReadOnlyList<HighlightSourceClipDto>> MapSourceClipsToDtoAsync(string? sourceSegmentsJson)
    {
        if (string.IsNullOrWhiteSpace(sourceSegmentsJson))
            return Array.Empty<HighlightSourceClipDto>();

        List<HighlightSourceClipGroup> groups;
        try
        {
            groups = HighlightVideoManifestHelper.DeserializeManifest(sourceSegmentsJson);
        }
        catch
        {
            return Array.Empty<HighlightSourceClipDto>();
        }

        var mediaIds = groups.Select(g => g.MediaId).Distinct().ToList();
        var mediaAssets = mediaIds.Count > 0
            ? await _unitOfWork.MediaAssets.GetAllAsync(m => mediaIds.Contains(m.Id))
            : new List<MediaAsset>();
        var mediaMap = mediaAssets.ToDictionary(m => m.Id);

        var activityIds = mediaAssets
            .Where(m => m.ActivityId.HasValue)
            .Select(m => m.ActivityId!.Value)
            .Distinct()
            .ToList();
        var activities = activityIds.Count > 0
            ? await _unitOfWork.Activities.GetAllAsync(a => activityIds.Contains(a.Id))
            : new List<Activity>();
        var activityMap = activities.ToDictionary(a => a.Id);

        return groups.Select(g =>
        {
            mediaMap.TryGetValue(g.MediaId, out var media);
            Activity? activity = null;
            if (media?.ActivityId is Guid activityId)
                activityMap.TryGetValue(activityId, out activity);

            return new HighlightSourceClipDto
            {
                MediaId = g.MediaId,
                ActivityId = media?.ActivityId,
                ActivityName = activity?.Name,
                Segments = g.Segments
                    .Select(s => new HighlightSourceSegmentDto
                    {
                        StartMs = s.StartMs,
                        EndMs = s.EndMs,
                        OutputStartMs = s.OutputStartMs,
                        OutputEndMs = s.OutputEndMs,
                    })
                    .ToList(),
            };
        }).ToList();
    }

}
