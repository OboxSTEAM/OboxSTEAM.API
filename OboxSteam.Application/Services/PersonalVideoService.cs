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
///             plus mapped voice segments (sole student + sole speaker).
///   Case 2 — Video has no other faces besides the target student → include ENTIRE video.
///   Case 3 — Video has multiple people → extract only the student's segments from the timeline
///             using 2-second buffers; segments that overlap after buffering are merged.
///   Fallback — No persisted timeline (legacy data / capture failure): include the ENTIRE video
///              only when the student is the sole tagged person; otherwise skip the video to
///              avoid leaking other people's faces.
///
/// Strengths Filtering (optional — triggered when caller supplies a strength description):
///   Cross-references on-camera face segments and off-camera voice-only windows with the
///   persisted Label Detection timeline via an LLM (AWS Bedrock). Only segments where visual
///   labels demonstrate the described strength are kept. Label data must be present for every
///   video that enters the strength path — missing timelines fail the job with an explicit reason
///   (no silent fallback to face-only / full-video clipping). When no segment matches across
///   all videos the background job is marked <see cref="HighlightVideoStatus.Failed"/>.
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
        string? FailureReason = null);

    // ── Clipping constants ───────────────────────────────────────────────────
    /// <summary>Padding before/after range segments (StartMs &lt; EndMs) from strength matching.</summary>
    private const long BufferMs = 2_000;

    /// <summary>
    /// Smaller padding for point detections (StartMs == EndMs) so a single label instant
    /// does not expand into a 4-second clip.
    /// </summary>
    private const long PointBufferMs = 1_000;

    /// <summary>After buffering, merge adjacent ranges when the gap between them is at most this.</summary>
    private const long MergeGapMs = 500;

    /// <summary>Labels within this gap (ms) are merged into one voice clip cluster.</summary>
    private const long LabelClusterMaxGapMs = 5_000;

    /// <summary>
    /// Trim when the LLM time range is at least this many times wider than the evidence-label
    /// cluster span (e.g. 1.5 → a 15 s LLM range shrinks if evidence spans ~10 s or less).
    /// </summary>
    private const double TrimWhenLlmSpanExceedsEvidenceRatio = 1.5;

    private const float MinLabelConfidenceForTrim = 60f;

    /// <summary>
    /// When filtering label-detection entries for Claude, include labels within this
    /// window around each face segment (before StartMs and after EndMs).
    /// 5 seconds of extra context helps Claude detect activities that start just
    /// before/after the student enters the frame.
    /// </summary>
    private const long LabelContextWindowMs = 5_000;

    private const string PersonalVideoFolder = "personal-videos";

    private const int MaxStacksPerStudentProgram = 3;
    private const int MaxItemsPerStack = 4;

    /// <summary>When false, LLM segment bounds are used as-is (user trims manually later).</summary>
    private const bool UseEvidenceTrim = false;

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
                    return MapStackToDto(existingStack, existingItems);
            }

            if (existingItems.Count >= MaxItemsPerStack)
                throw ErrorHelper.Conflict(
                    $"Stack already has {MaxItemsPerStack} videos. Delete an item before generating again.");

            await CreateAndEnqueueInitialItemAsync(existingStack, normalizedStrength);
            var refreshedItems = await LoadStackItemsAsync(existingStack.Id);
            return MapStackToDto(existingStack, refreshedItems);
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
        return MapStackToDto(stack, items);
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
            result.Add(MapStackToDto(stack, items));
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
        return MapStackToDto(stack, items);
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

        if (string.IsNullOrWhiteSpace(parent.OutputS3Key) || parent.DurationMs is null or <= 0)
            throw ErrorHelper.BadRequest("Parent video output metadata is missing; cannot trim.");

        if (request.ExcludeRanges.Count == 0)
            throw ErrorHelper.BadRequest("At least one exclude range is required.");

        var excludeRanges = request.ExcludeRanges
            .Select(r => (
                StartMs: HighlightVideoTimeHelper.ParseTimecodeToMs(r.Start),
                EndMs: HighlightVideoTimeHelper.ParseTimecodeToMs(r.End)))
            .ToList();

        foreach (var (start, end) in excludeRanges)
        {
            if (start < 0 || end > parent.DurationMs.Value || end <= start)
                throw ErrorHelper.BadRequest(
                    $"Exclude range must lie within 00:00:00 and the video duration ({parent.DurationMs}ms).");
        }

        HighlightVideoTimeHelper.ComputeKeepSegments(parent.DurationMs.Value, excludeRanges);

        var trimItem = new HighlightVideoItem
        {
            StackId = stack.Id,
            ParentItemId = parent.Id,
            GenerationKind = HighlightVideoGenerationKind.Trim,
            Status = HighlightVideoStatus.Processing,
            RequestedAt = DateTime.UtcNow,
            TrimDescription = request.TrimDescription,
            TrimExcludeRangesJson = JsonSerializer.Serialize(request.ExcludeRanges),
        };
        await _unitOfWork.HighlightVideoItems.AddAsync(trimItem);
        await _unitOfWork.SaveChangesAsync();

        _queue.Enqueue(new PersonalVideoJob(
            trimItem.Id,
            PersonalVideoJobKind.OutputTrim,
            programId,
            studentId,
            StrengthDescription: null,
            ParentOutputS3Key: parent.OutputS3Key,
            ParentDurationMs: parent.DurationMs,
            ExcludeRanges: excludeRanges
                .Select(r => new OutputExcludeRange(r.StartMs, r.EndMs))
                .ToList()));

        return MapItemToDto(trimItem, request.ExcludeRanges);
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
            if (job.Kind == PersonalVideoJobKind.OutputTrim)
            {
                await ProcessOutputTrimAsync(item, job);
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
            strengthForJob,
            ParentOutputS3Key: null,
            ParentDurationMs: null,
            ExcludeRanges: null));

        _logger.LogInformation(
            "[PersonalVideoService] Initial generation queued. StackId={StackId}, ItemId={ItemId}",
            stack.Id, item.Id);

        return item;
    }

    private async Task ProcessInitialGenerationAsync(HighlightVideoItem item, PersonalVideoJob job)
    {
        var buildResult = await BuildClipInputsAsync(job.ProgramId, job.StudentId, job.StrengthDescription);

        if (buildResult.FailureReason != null)
        {
            _logger.LogWarning(
                "[PersonalVideoService] Clip build failed for item {Id}: {Reason}",
                item.Id, buildResult.FailureReason);

            item.Status = HighlightVideoStatus.Failed;
            item.FailureReason = buildResult.FailureReason;
            await _unitOfWork.SaveChangesAsync();
            return;
        }

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

        var outputKey = $"{PersonalVideoFolder}/{job.StudentId}_{item.Id}_{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}.mp4";
        var mcJobId = await _videoConverterService.SubmitPersonalVideoJobAsync(clipInputs.ToList(), outputKey);

        item.PersonalVideoJobRef = mcJobId;
        item.Status = HighlightVideoStatus.Processing;
        item.FailureReason = null;
        await _unitOfWork.SaveChangesAsync();
    }

    private async Task ProcessOutputTrimAsync(HighlightVideoItem item, PersonalVideoJob job)
    {
        if (string.IsNullOrWhiteSpace(job.ParentOutputS3Key) || job.ParentDurationMs is null or <= 0)
        {
            item.Status = HighlightVideoStatus.Failed;
            item.FailureReason = "Trim job is missing parent output metadata.";
            await _unitOfWork.SaveChangesAsync();
            return;
        }

        if (job.ExcludeRanges is not { Count: > 0 })
        {
            item.Status = HighlightVideoStatus.Failed;
            item.FailureReason = "Trim job is missing exclude ranges.";
            await _unitOfWork.SaveChangesAsync();
            return;
        }

        var excludeTuples = job.ExcludeRanges
            .Select(r => (r.StartMs, r.EndMs))
            .ToList();
        var keepSegments = HighlightVideoTimeHelper.ComputeKeepSegments(job.ParentDurationMs.Value, excludeTuples);
        var keepClips = HighlightVideoTimeHelper.ToTimeClips(keepSegments);

        var stack = await _unitOfWork.HighlightVideoStacks.GetByIdAsync(item.StackId)
            ?? throw new InvalidOperationException($"Stack {item.StackId} not found for trim item.");

        var outputKey =
            $"{PersonalVideoFolder}/{stack.StudentId}_{item.Id}_{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}.mp4";
        var mcJobId = await _videoConverterService.SubmitOutputTrimJobAsync(
            job.ParentOutputS3Key, keepClips, outputKey);

        item.PersonalVideoJobRef = mcJobId;
        item.Status = HighlightVideoStatus.Processing;
        item.FailureReason = null;
        item.DurationMs = keepSegments.Sum(s => s.EndMs - s.StartMs);
        await _unitOfWork.SaveChangesAsync();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Logic Core
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Traverses Program → Module → Course → Activity → MediaAsset to collect all
    /// <c>TaggingComplete</c> video assets that have a <see cref="MediaTag"/> for
    /// <paramref name="studentId"/>, then applies the Logic Core rules to build the
    /// ordered list of <see cref="ClipInput"/> objects for the MediaConvert job.
    /// When <paramref name="strengthDescription"/> is provided, each video's face and voice-only
    /// segments are filtered via Bedrock against the label detection timeline. Missing label data
    /// for any such video fails the whole build with an explicit <see cref="ClipBuildResult.FailureReason"/>.
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
            .OrderBy(m => m.UploadedAt)
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
                clips.Add(result.Clip);
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

        return new ClipBuildResult(clips);
    }

    /// <summary>
    /// Applies Logic Core rules to a single <see cref="MediaAsset"/> and returns the
    /// corresponding <see cref="ClipInput"/> (with or without <see cref="TimeClip"/>s).
    /// When <paramref name="strengthDescription"/> is provided, on-camera face segments and
    /// off-camera voice-only windows are cross-referenced against the Label Detection timeline
    /// via Bedrock before being used as clips.
    /// Returns a skipped video (<c>Clip</c> null, no error) when strengths filtering yields no
    /// matched segments. Returns <see cref="StrengthFilterError.LabelUnavailable"/> or
    /// <see cref="StrengthFilterError.MatchingFailed"/> when strength filtering cannot run.
    /// </summary>
    private async Task<MediaClipBuildResult> BuildClipInputForMediaAsync(
        MediaAsset media, string s3Key, Guid studentId, string? strengthDescription = null)
    {
        var voiceSegs = ReadVoiceSegments(media, studentId);
        _logger.LogInformation(
            "[PersonalVideoService] BuildClipInputForMediaAsync entry: MediaId={MediaId}, StudentId={StudentId}, " +
            "S3Key={S3Key}, HasStrength={HasStrength}, VoiceSegments={VoiceCount}",
            media.Id, studentId, s3Key, !string.IsNullOrWhiteSpace(strengthDescription), voiceSegs.Count);

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
            {
                var filteredResult = await ApplyStrengthsFilterAsync(
                    media, s3Key, studentId, timelineResult.Segments, strengthDescription);

                if (filteredResult.Error == StrengthFilterError.None
                    && filteredResult.Clip != null
                    && filteredResult.Clip.Clips.Count > 0)
                {
                    _logger.LogInformation(
                        "[PersonalVideoService] Case 1 + strengths: {Count} sub-clip(s). MediaId={MediaId}",
                        filteredResult.Clip.Clips.Count, media.Id);
                    return new MediaClipBuildResult(filteredResult.Clip);
                }

                _logger.LogWarning(
                    "[PersonalVideoService] Case 1 + strengths: no LLM match → full video fallback. MediaId={MediaId}",
                    media.Id);
            }

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
            if (!string.IsNullOrWhiteSpace(strengthDescription) && timelineResult.Segments.Count > 0)
            {
                var filteredResult = await ApplyStrengthsFilterAsync(
                    media, s3Key, studentId, timelineResult.Segments, strengthDescription);

                if (filteredResult.Error == StrengthFilterError.None
                    && filteredResult.Clip != null
                    && filteredResult.Clip.Clips.Count > 0)
                    return new MediaClipBuildResult(filteredResult.Clip);

                _logger.LogWarning(
                    "[PersonalVideoService] Case 2 + strengths: no LLM match → full video fallback. MediaId={MediaId}",
                    media.Id);
            }

            return new MediaClipBuildResult(new ClipInput(s3Key, new List<TimeClip>()));
        }

        // ── Case 3 & 4: Multiple people — extract student's timeline ──────────────
        _logger.LogInformation(
            "[PersonalVideoService] Case 3 & 4 (mixed faces per persisted timeline) → extracting timeline. MediaId={MediaId}", media.Id);

        var faceSegments = timelineResult.Segments;

        // ── Strengths Filtering (optional) ────────────────────────────────────────
        if (!string.IsNullOrWhiteSpace(strengthDescription))
        {
            var filteredResult = await ApplyStrengthsFilterAsync(
                media, s3Key, studentId, faceSegments, strengthDescription);

            if (filteredResult.Error == StrengthFilterError.None
                && filteredResult.Clip != null
                && filteredResult.Clip.Clips.Count > 0)
                return new MediaClipBuildResult(filteredResult.Clip);

            _logger.LogWarning(
                "[PersonalVideoService] Case 3/4 + strengths: no LLM match → face/voice fallback. MediaId={MediaId}",
                media.Id);
        }

        // Standard face-timeline path (no strengths).
        // Union the student's face timeline with their mapped voice timeline (if available) so
        // the highlight keeps "voice but no face" moments (e.g. the student speaking off-camera).
        // MergeAndFormatTimeClips collapses any overlaps created by the union.
        var voiceSegments = ReadVoiceSegments(media, studentId);
        var combinedSegments = faceSegments
            .Concat(voiceSegments)
            .Select(s => new MatchedSegment(s.StartMs, s.EndMs, "", 0, Array.Empty<string>()));

        var timeClips = MergeAndFormatTimeClips(combinedSegments);

        _logger.LogInformation(
            "[PersonalVideoService] Case 3/4: {Face} face + {Voice} voice → {Merged} merged segment(s) for MediaId={MediaId}. Clips=[{Clips}]",
            faceSegments.Count, voiceSegments.Count, timeClips.Count, media.Id,
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
    /// Reads the student's mapped voice timeline from <see cref="MediaTag.VoiceSegmentsJson"/>
    /// (populated at tagging time when AWS Transcribe diarization mapped a speaker to this
    /// student). Returns an empty list when no voice data was captured (legacy media, Transcribe
    /// disabled/failed, or no speaker could be mapped) so callers degrade to face-only clipping.
    /// </summary>
    private List<FaceTimestampSegment> ReadVoiceSegments(MediaAsset media, Guid studentId)
    {
        var tag = media.MediaTags.FirstOrDefault(t => t.StudentId == studentId);
        if (tag?.VoiceSegmentsJson == null)
            return new List<FaceTimestampSegment>();

        try
        {
            return JsonSerializer.Deserialize<List<FaceTimestampSegment>>(tag.VoiceSegmentsJson)
                   ?? new List<FaceTimestampSegment>();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "[PersonalVideoService] Failed to deserialize VoiceSegmentsJson for MediaId={MediaId}, StudentId={StudentId}.",
                media.Id, studentId);
            return new List<FaceTimestampSegment>();
        }
    }

    /// <summary>
    /// Loads the Label Detection timeline for <paramref name="media"/> then calls
    /// <see cref="IStrengthMatchService.MatchStrengthsAsync"/> for on-camera face windows,
    /// <see cref="IStrengthMatchService.MatchStrengthsFromLabelsOnlyAsync"/> when there are no
    /// face segments (Case 1), and <see cref="IStrengthMatchService.MatchStrengthsForVoiceOnlyAsync"/>
    /// for off-camera voice windows (same ±<see cref="LabelContextWindowMs"/> label context as face).
    /// </summary>
    /// <returns>
    /// <see cref="StrengthFilterError.None"/> with a populated <c>Clip</c> on success;
    /// <see cref="StrengthFilterError.NoMatch"/> with an empty clip when nothing matched;
    /// <see cref="StrengthFilterError.LabelUnavailable"/> or <see cref="StrengthFilterError.MatchingFailed"/>
    /// when strength filtering cannot be honoured.
    /// </returns>
    private async Task<StrengthFilterResult> ApplyStrengthsFilterAsync(
        MediaAsset media, string s3Key, Guid studentId,
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
            return new StrengthFilterResult(null, StrengthFilterError.LabelUnavailable, detail);
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
            return new StrengthFilterResult(null, StrengthFilterError.LabelUnavailable, detail);
        }

        if (labelTimeline == null || labelTimeline.Count == 0)
        {
            var detail = $"MediaId={media.Id}: label detection timeline is empty.";
            _logger.LogWarning(
                "[PersonalVideoService] Persisted label timeline is empty for MediaId={MediaId}.",
                media.Id);
            return new StrengthFilterResult(
                new ClipInput(s3Key, new List<TimeClip>()),
                StrengthFilterError.NoMatch,
                detail);
        }

        // ── Token optimisation ────────────────────────────────────────────────
        // Scene-only (Case 1): no face windows — scan full label timeline via label-only Bedrock.
        // Otherwise send labels within face windows and, separately, within off-camera voice windows.
        var isSceneOnly = faceSegments.Count == 0;
        var allVoiceSegments = ReadVoiceSegments(media, studentId);
        var voiceOnlySegments = GetVoiceOnlySegments(faceSegments, allVoiceSegments);
        var faceRelevantLabels = isSceneOnly
            ? new List<LabelDetectionEntry>()
            : FilterLabelsToSegmentWindows(labelTimeline, faceSegments);
        var voiceRelevantLabels = voiceOnlySegments.Count > 0
            ? FilterLabelsToSegmentWindows(labelTimeline, voiceOnlySegments)
            : new List<LabelDetectionEntry>();

        _logger.LogInformation(
            "[PersonalVideoService] ApplyStrengthsFilter path: MediaId={MediaId}, sceneOnly={SceneOnly}, " +
            "faceSegments={FaceCount}, totalVoice={TotalVoice}, voiceOnly={VoiceOnlyCount}, " +
            "totalLabels={TotalLabels}, faceLabels={FaceLabels}, voiceLabels={VoiceLabels}",
            media.Id, isSceneOnly, faceSegments.Count, allVoiceSegments.Count,
            voiceOnlySegments.Count, labelTimeline.Count, faceRelevantLabels.Count, voiceRelevantLabels.Count);

        if (!isSceneOnly && faceRelevantLabels.Count == 0 && voiceRelevantLabels.Count == 0)
        {
            _logger.LogInformation(
                "[PersonalVideoService] No labels within face or voice-only windows for MediaId={MediaId}. Skipping video.",
                media.Id);
            return new StrengthFilterResult(
                new ClipInput(s3Key, new List<TimeClip>()),
                StrengthFilterError.NoMatch);
        }

        _logger.LogInformation(
            "[PersonalVideoService] Label timeline filtered for MediaId={MediaId}: sceneOnly={SceneOnly}, face {FaceLabels} label(s), voice-only {VoiceLabels} label(s).",
            media.Id, isSceneOnly, faceRelevantLabels.Count, voiceRelevantLabels.Count);

        var allMatched = new List<MatchedSegment>();
        var faceMatched = new List<MatchedSegment>();
        string reasoning = "";

        // ── Scene-only: full label timeline (no face constraint) ──────────────
        if (isSceneOnly)
        {
            try
            {
                var labelOnlyMatch = await _strengthMatchService.MatchStrengthsFromLabelsOnlyAsync(
                    labelTimeline, strengthDescription);
                var trimmedLabelOnly = MaybeTrimEvidenceLabels(
                    labelOnlyMatch.MatchedSegments, labelTimeline, media.Id, "label-only");
                allMatched.AddRange(trimmedLabelOnly);
                reasoning = labelOnlyMatch.Reasoning;
            }
            catch (Exception ex)
            {
                if (voiceRelevantLabels.Count == 0)
                {
                    var detail = $"MediaId={media.Id}: label-only strength matching failed (scene-only / Case 1).";
                    _logger.LogWarning(ex,
                        "[PersonalVideoService] Label-only strength matching failed for MediaId={MediaId}.",
                        media.Id);
                    return new StrengthFilterResult(null, StrengthFilterError.MatchingFailed, detail);
                }

                _logger.LogWarning(ex,
                    "[PersonalVideoService] Label-only strength matching failed for MediaId={MediaId}; continuing with voice-only path.",
                    media.Id);
            }
        }

        // ── On-camera: face segments + labels in face windows ─────────────────
        if (!isSceneOnly && faceRelevantLabels.Count > 0)
        {
            try
            {
                var faceMatch = await _strengthMatchService.MatchStrengthsAsync(
                    faceSegments, faceRelevantLabels, strengthDescription);
                var trimmedFace = MaybeTrimEvidenceLabels(
                    faceMatch.MatchedSegments, labelTimeline, media.Id, "face");
                faceMatched.AddRange(trimmedFace);
                allMatched.AddRange(trimmedFace);
                reasoning = faceMatch.Reasoning;
            }
            catch (Exception ex)
            {
                if (voiceRelevantLabels.Count == 0)
                {
                    var detail = $"MediaId={media.Id}: strength matching failed for on-camera segments.";
                    _logger.LogWarning(ex,
                        "[PersonalVideoService] Claude strength matching (face) failed for MediaId={MediaId}.",
                        media.Id);
                    return new StrengthFilterResult(null, StrengthFilterError.MatchingFailed, detail);
                }

                _logger.LogWarning(ex,
                    "[PersonalVideoService] Claude strength matching (face) failed for MediaId={MediaId}; continuing with voice-only path.",
                    media.Id);
            }
        }

        // ── Off-camera: voice windows + visual labels in wider padded windows ──
        if (voiceOnlySegments.Count > 0 && voiceRelevantLabels.Count > 0)
        {
            try
            {
                var voiceMatch = await _strengthMatchService.MatchStrengthsForVoiceOnlyAsync(
                    voiceOnlySegments, voiceRelevantLabels, strengthDescription);
                var trimmedVoice = MaybeTrimEvidenceLabels(
                    voiceMatch.MatchedSegments, labelTimeline, media.Id, "voice", voiceOnlySegments);
                var voiceKept = PreferFaceOverOverlappingVoice(faceMatched, trimmedVoice, media.Id);
                allMatched.AddRange(voiceKept);
                if (!string.IsNullOrWhiteSpace(voiceMatch.Reasoning))
                    reasoning = string.IsNullOrWhiteSpace(reasoning)
                        ? voiceMatch.Reasoning
                        : $"{reasoning} | voice-only: {voiceMatch.Reasoning}";
            }
            catch (Exception ex)
            {
                // Non-fatal: keep any on-camera matches already collected.
                _logger.LogWarning(ex,
                    "[PersonalVideoService] Claude strength matching (voice-only) failed for MediaId={MediaId}. Using face matches only.",
                    media.Id);
            }
        }

        _logger.LogInformation(
            "[PersonalVideoService] ApplyStrengthsFilter result: MediaId={MediaId}, faceMatched={FaceMatched}, totalMatched={TotalMatched}",
            media.Id, faceMatched.Count, allMatched.Count);

        if (allMatched.Count == 0)
        {
            _logger.LogInformation(
                "[PersonalVideoService] No strength matches (face or voice-only) for MediaId={MediaId}. Reasoning: {Reasoning}",
                media.Id, reasoning);
            return new StrengthFilterResult(
                new ClipInput(s3Key, new List<TimeClip>()),
                StrengthFilterError.NoMatch);
        }

        var timeClips = MergeAndFormatTimeClips(allMatched);

        _logger.LogInformation(
            "[PersonalVideoService] Strengths filter: {Count} clip(s) for MediaId={MediaId}. Clips=[{Clips}]. Reasoning: {Reasoning}",
            timeClips.Count, media.Id,
            string.Join(", ", timeClips.Select(c => $"{c.StartTimecode}→{c.EndTimecode}")),
            reasoning);

        return new StrengthFilterResult(new ClipInput(s3Key, timeClips));
    }

    /// <summary>
    /// Returns voice segments that do not overlap any face segment — i.e. the student is
    /// speaking off-camera. These are evaluated separately by <see cref="ApplyStrengthsFilterAsync"/>
    /// using labels that fall within the voice-only time windows.
    /// </summary>
    private List<FaceTimestampSegment> GetVoiceOnlySegments(
        IList<FaceTimestampSegment> faceSegments,
        IList<FaceTimestampSegment> voiceSegments)
    {
        if (voiceSegments.Count == 0)
            return new List<FaceTimestampSegment>();

        var result = voiceSegments
            .Where(v => !faceSegments.Any(f => SegmentsOverlap(f, v)))
            .ToList();

        _logger.LogDebug(
            "[PersonalVideoService] GetVoiceOnlySegments: {InputVoice} voice segment(s) in, {FaceCount} face segment(s), {OutputVoice} voice-only survived (dropped {Dropped} overlapping)",
            voiceSegments.Count, faceSegments.Count, result.Count, voiceSegments.Count - result.Count);

        return result;
    }

    private static bool SegmentsOverlap(FaceTimestampSegment a, FaceTimestampSegment b)
    {
        // Inclusive: Rekognition face samples are often single points (StartMs == EndMs).
        var overlap = Math.Min(a.EndMs, b.EndMs) - Math.Max(a.StartMs, b.StartMs);
        return overlap >= 0;
    }

    /// <summary>
    /// Drops voice matches that temporally overlap any on-camera face match — face is the
    /// stronger signal when both paths would produce clips for the same moment.
    /// </summary>
    private List<MatchedSegment> PreferFaceOverOverlappingVoice(
        IList<MatchedSegment> faceMatches,
        IList<MatchedSegment> voiceMatches,
        Guid mediaId)
    {
        if (faceMatches.Count == 0)
            return voiceMatches.ToList();

        var kept = new List<MatchedSegment>(voiceMatches.Count);
        foreach (var voice in voiceMatches)
        {
            if (faceMatches.Any(f => SegmentsOverlapMs(f.StartMs, f.EndMs, voice.StartMs, voice.EndMs)))
            {
                _logger.LogInformation(
                    "[PersonalVideoService] Voice match dropped (overlaps face): [{Start}ms→{End}ms] score={Score:0.00} MediaId={MediaId}",
                    voice.StartMs, voice.EndMs, voice.Score, mediaId);
                continue;
            }

            kept.Add(voice);
        }

        return kept;
    }

    /// <summary>
    /// Converts matched segments into AWS MediaConvert-compatible <see cref="TimeClip"/>s:
    /// applies per-segment buffer (smaller for point detections), merges overlaps and small
    /// gaps in millisecond space, then formats strictly ascending non-overlapping timecodes.
    /// </summary>
    private static List<TimeClip> MergeAndFormatTimeClips(IEnumerable<MatchedSegment> segments)
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
            .Select(r => new TimeClip(MsToTimecode(r.Start), MsToTimecode(r.End)))
            .Where(t => t.StartTimecode != t.EndTimecode)
            .ToList();
    }

    /// <summary>
    /// Shrinks LLM segment bounds to Rekognition timestamps for the model's
    /// <see cref="MatchedSegment.EvidenceLabels"/> when the LLM range is much wider than that
    /// evidence cluster. Semantic matching stays with the LLM; this step is geometry only.
    /// </summary>
    private List<MatchedSegment> TrimMatchesToEvidenceLabels(
        IList<MatchedSegment> matches,
        IList<LabelDetectionEntry> labelTimeline,
        Guid mediaId,
        string path,
        IList<FaceTimestampSegment>? clampWindows = null)
    {
        var result = new List<MatchedSegment>();

        foreach (var match in matches)
        {
            var searchStart = match.StartMs;
            var searchEnd = match.EndMs;

            FaceTimestampSegment? clampWindow = null;
            if (clampWindows is { Count: > 0 })
            {
                clampWindow = clampWindows.FirstOrDefault(v =>
                    SegmentsOverlapMs(v.StartMs, v.EndMs, match.StartMs, match.EndMs));

                if (clampWindow == null)
                {
                    _logger.LogWarning(
                        "[PersonalVideoService] Evidence trim ({Path}): no clamp window for [{Start}ms→{End}ms] MediaId={MediaId}; keeping LLM bounds",
                        path, match.StartMs, match.EndMs, mediaId);
                    result.Add(match);
                    continue;
                }

                _logger.LogDebug(
                    "[PersonalVideoService] Evidence trim ({Path}): clamping [{OrigStart}ms→{OrigEnd}ms] to window [{WinStart}ms→{WinEnd}ms] MediaId={MediaId}",
                    path, match.StartMs, match.EndMs, clampWindow.StartMs, clampWindow.EndMs, mediaId);

                searchStart = Math.Max(searchStart, clampWindow.StartMs);
                searchEnd = Math.Min(searchEnd, clampWindow.EndMs);
            }

            if (searchStart > searchEnd)
                continue;

            var llmSpan = searchEnd - searchStart;
            var evidenceNames = match.EvidenceLabels
                .Where(n => !string.IsNullOrWhiteSpace(n))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (evidenceNames.Count == 0)
            {
                _logger.LogWarning(
                    "[PersonalVideoService] Evidence trim ({Path}): no evidence_labels on match [{Start}ms→{End}ms] MediaId={MediaId}; keeping LLM bounds",
                    path, searchStart, searchEnd, mediaId);
                result.Add(match with { StartMs = searchStart, EndMs = searchEnd });
                continue;
            }

            var evidenceSet = new HashSet<string>(evidenceNames, StringComparer.OrdinalIgnoreCase);
            var evidenceInRange = labelTimeline
                .Where(l => l.TimestampMs >= searchStart
                            && l.TimestampMs <= searchEnd
                            && l.Confidence >= MinLabelConfidenceForTrim
                            && evidenceSet.Contains(l.LabelName))
                .ToList();

            if (evidenceInRange.Count == 0)
            {
                if (path == "voice" && clampWindow != null)
                {
                    var voiceSpan = clampWindow.EndMs - clampWindow.StartMs;
                    var coversFullVoice = voiceSpan > 0 && llmSpan >= voiceSpan * 0.85;
                    if (coversFullVoice)
                    {
                        _logger.LogInformation(
                            "[PersonalVideoService] Evidence trim REJECTED ({Path}) [{Start}ms→{End}ms] MediaId={MediaId}: full voice span, evidence [{Evidence}] not found in timeline",
                            path, searchStart, searchEnd, mediaId, string.Join(", ", evidenceNames));
                        continue;
                    }
                }

                _logger.LogWarning(
                    "[PersonalVideoService] Evidence trim ({Path}): evidence [{Evidence}] not found in [{Start}ms→{End}ms] MediaId={MediaId}; keeping LLM bounds",
                    path, string.Join(", ", evidenceNames), searchStart, searchEnd, mediaId);
                result.Add(match with { StartMs = searchStart, EndMs = searchEnd });
                continue;
            }

            var clusters = ClusterLabelsByTime(evidenceInRange, LabelClusterMaxGapMs);
            _logger.LogDebug(
                "[PersonalVideoService] Evidence trim ({Path}): {ClusterCount} cluster(s) from {EvidenceCount} evidence label(s) in [{Start}ms→{End}ms] MediaId={MediaId}. " +
                "Cluster spans: [{Spans}]",
                path, clusters.Count, evidenceInRange.Count, searchStart, searchEnd, mediaId,
                string.Join(", ", clusters.Select(c => $"{c.Min(l => l.TimestampMs)}→{c.Max(l => l.TimestampMs)}ms")));
            var trimmedAny = false;

            foreach (var cluster in clusters)
            {
                var clusterStart = cluster.Min(l => l.TimestampMs);
                var clusterEnd = cluster.Max(l => l.TimestampMs);
                var clusterSpan = clusterEnd - clusterStart;
                var shouldTrim = llmSpan > Math.Max(clusterSpan, 1) * TrimWhenLlmSpanExceedsEvidenceRatio;

                if (!shouldTrim)
                    continue;

                var outStart = clusterStart;
                var outEnd = clusterEnd;
                if (clampWindow != null)
                {
                    outStart = Math.Max(outStart, clampWindow.StartMs);
                    outEnd = Math.Min(outEnd, clampWindow.EndMs);
                }

                if (outStart > outEnd)
                    continue;

                trimmedAny = true;
                result.Add(new MatchedSegment(outStart, outEnd, match.Strength, match.Score, evidenceNames));

                _logger.LogInformation(
                    "[PersonalVideoService] Evidence trim ({Path}) [{LlmStart}ms→{LlmEnd}ms] span={LlmSpan}ms → [{Start}ms→{End}ms] span={OutSpan}ms MediaId={MediaId} evidence=[{Evidence}]",
                    path, searchStart, searchEnd, llmSpan, outStart, outEnd, outEnd - outStart, mediaId,
                    string.Join(", ", evidenceNames));
            }

            if (!trimmedAny)
            {
                result.Add(match with { StartMs = searchStart, EndMs = searchEnd });
                _logger.LogInformation(
                    "[PersonalVideoService] Evidence trim ({Path}): LLM range already tight [{Start}ms→{End}ms] MediaId={MediaId} evidence=[{Evidence}]",
                    path, searchStart, searchEnd, mediaId, string.Join(", ", evidenceNames));
            }
        }

        return result;
    }

    private List<MatchedSegment> MaybeTrimEvidenceLabels(
        IList<MatchedSegment> matches,
        IList<LabelDetectionEntry> labelTimeline,
        Guid mediaId,
        string path,
        IList<FaceTimestampSegment>? clampWindows = null) =>
        UseEvidenceTrim
            ? TrimMatchesToEvidenceLabels(matches, labelTimeline, mediaId, path, clampWindows)
            : matches.ToList();

    private static List<List<LabelDetectionEntry>> ClusterLabelsByTime(
        IList<LabelDetectionEntry> labels,
        long maxGapMs)
    {
        if (labels.Count == 0)
            return new List<List<LabelDetectionEntry>>();

        var sorted = labels.OrderBy(l => l.TimestampMs).ToList();
        var clusters = new List<List<LabelDetectionEntry>> { new() { sorted[0] } };

        foreach (var label in sorted.Skip(1))
        {
            var lastTs = clusters[^1][^1].TimestampMs;
            if (label.TimestampMs - lastTs <= maxGapMs)
                clusters[^1].Add(label);
            else
                clusters.Add(new List<LabelDetectionEntry> { label });
        }

        return clusters;
    }

    private static bool SegmentsOverlapMs(long aStart, long aEnd, long bStart, long bEnd) =>
        Math.Min(aEnd, bEnd) - Math.Max(aStart, bStart) >= 0;


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
    /// Converts milliseconds to AWS MediaConvert InputClipping timecode format: <c>HH:MM:SS:00</c>
    /// using TimecodeSource.ZEROBASED. Rounds to the nearest second.
    /// </summary>
    private static string MsToTimecode(long totalMs)
    {
        // Round to nearest second (+500 before integer division).
        var totalSec = (totalMs + 500) / 1000;
        var sec = (int)(totalSec % 60);
        var min = (int)(totalSec / 60 % 60);
        var hr = (int)(totalSec / 3_600);
        return $"{hr:D2}:{min:D2}:{sec:D2}:00";
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

    private static HighlightVideoStackDto MapStackToDto(
        HighlightVideoStack stack,
        IReadOnlyList<HighlightVideoItem> items) =>
        new()
        {
            Id = stack.Id,
            ProgramId = stack.ProgramId,
            StudentId = stack.StudentId,
            StrengthDescription = string.IsNullOrEmpty(stack.StrengthDescription)
                ? null
                : stack.StrengthDescription,
            CreatedAt = stack.CreatedAt,
            Items = items.Select(i => MapItemToDto(i)).ToList(),
        };

    private static HighlightVideoItemDto MapItemToDto(
        HighlightVideoItem item,
        IReadOnlyList<TimeRangeDto>? excludeRanges = null)
    {
        IReadOnlyList<TimeRangeDto>? ranges = excludeRanges;
        if (ranges == null && !string.IsNullOrWhiteSpace(item.TrimExcludeRangesJson))
        {
            ranges = JsonSerializer.Deserialize<List<TimeRangeDto>>(item.TrimExcludeRangesJson)
                     ?? new List<TimeRangeDto>();
        }

        return new HighlightVideoItemDto
        {
            Id = item.Id,
            StackId = item.StackId,
            ParentItemId = item.ParentItemId,
            GenerationKind = item.GenerationKind,
            VideoUrl = item.VideoUrl,
            DurationMs = item.DurationMs,
            Status = item.Status,
            RequestedAt = item.RequestedAt,
            FailureReason = item.FailureReason,
            TrimDescription = item.TrimDescription,
            TrimExcludeRanges = ranges,
        };
    }

}
