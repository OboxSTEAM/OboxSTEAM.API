using System.Text.Json;
using Microsoft.Extensions.Logging;
using OboxSteam.Application.DTOs.MediaDTO;
using OboxSteam.Application.Interfaces;
using OboxSteam.Application.Utils;
using OboxSteam.Domain.Entities;
using OboxSteam.Domain.Enums;
using OboxSteam.Domain.Interfaces;

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
///             footage) but was tagged for the student → include the ENTIRE video (Fallback).
///   Case 2 — Video has no other faces besides the target student → include ENTIRE video.
///   Case 3 — Video has multiple people → extract only the student's segments from the timeline
///             using 2-second buffers; segments that overlap after buffering are merged.
///   Fallback — No persisted timeline (legacy data / capture failure): include the ENTIRE video
///              only when the student is the sole tagged person; otherwise skip the video to
///              avoid leaking other people's faces.
///
/// Strengths Filtering (optional — triggered when caller supplies a strength description):
///   Cross-references face segments with the persisted Label Detection timeline via an LLM
///   (AWS Bedrock). Only segments where the student is performing a matched strength are kept.
///   If no match is found across all videos a BadRequest is thrown.
/// </summary>
public class PersonalVideoService : IPersonalVideoService
{
    // ── Clipping constants ───────────────────────────────────────────────────
    /// <summary>Milliseconds of padding added before/after each detected face segment.</summary>
    private const long BufferMs = 2_000;

    /// <summary>
    /// When filtering label-detection entries for Claude, include labels within this
    /// window around each face segment (before StartMs and after EndMs).
    /// 5 seconds of extra context helps Claude detect activities that start just
    /// before/after the student enters the frame.
    /// </summary>
    private const long LabelContextWindowMs = 5_000;

    private const string PersonalVideoFolder = "personal-videos";

    /// <summary>
    /// Sentinel "end" timestamp (ms) for a synthetic full-video segment used in Case 1
    /// (no recorded face segments) + strengths filtering. Divided by 1000 so that later
    /// buffer additions (+<see cref="BufferMs"/>) cannot overflow <see cref="long"/>.
    /// </summary>
    private const long FullVideoEndMs = long.MaxValue / 1_000;

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
    public async Task<HighlightVideoDto> TriggerPersonalVideoGenerationAsync(
        Guid programId, Guid studentId, string? strengthDescription = null)
    {
        _logger.LogInformation(
            "[PersonalVideoService] TriggerPersonalVideoGenerationAsync: ProgramId={ProgramId}, StudentId={StudentId}, StrengthDescription={StrengthDescription}",
            programId, studentId, strengthDescription);

        // ── 1. Validate program & student ────────────────────────────────────
        var program = await _unitOfWork.Programs.GetByIdAsync(programId);
        if (program == null || program.IsDeleted)
            throw ErrorHelper.NotFound($"Program '{programId}' not found.");

        var student = await _unitOfWork.Users.GetByIdAsync(studentId);
        if (student == null || student.IsDeleted)
            throw ErrorHelper.NotFound($"Student '{studentId}' not found.");

        // ── 2. Guard: do not re-trigger while already processing ────────────
        var existing = await _unitOfWork.HighlightVideos.FirstOrDefaultAsync(
            hv => hv.ProgramId == programId && hv.StudentId == studentId && !hv.IsDeleted);

        if (existing?.PersonalVideoStatus == HighlightVideoStatus.Processing)
        {
            var requestedAt = existing.PersonalVideoRequestedAt ?? existing.CreatedAt;
            var isStale = DateTime.UtcNow - requestedAt > ProcessingStaleThreshold;

            if (!isStale)
            {
                _logger.LogInformation(
                    "[PersonalVideoService] Job already in progress for ProgramId={ProgramId}, StudentId={StudentId}",
                    programId, studentId);
                return MapToDto(existing);
            }

            // The previous job has been Processing past the stale threshold — assume its
            // completion webhook was lost and allow a fresh job to be submitted below.
            _logger.LogWarning(
                "[PersonalVideoService] Existing Processing job is stale (requested at {RequestedAt:o}); " +
                "allowing re-trigger. ProgramId={ProgramId}, StudentId={StudentId}",
                requestedAt, programId, studentId);
        }

        // ── 3. Create / reset the HighlightVideo record in Processing state ──
        //
        // The record is persisted as Processing BEFORE any heavy work (clip building, Bedrock,
        // MediaConvert). This lets the API return 202 immediately (no request-thread timeout)
        // and — together with the guard above and the unique (ProgramId, StudentId) index —
        // collapses the window for duplicate job submission from rapid double-clicks.
        if (existing == null)
        {
            existing = new HighlightVideo
            {
                StudentId = studentId,
                ProgramId = programId,
                PersonalVideoStatus = HighlightVideoStatus.Processing,
                PersonalVideoRequestedAt = DateTime.UtcNow,
            };
            await _unitOfWork.HighlightVideos.AddAsync(existing);

            try
            {
                await _unitOfWork.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                // A concurrent request inserted the row first → unique index violation.
                // Return that winning record instead of submitting a duplicate job.
                _logger.LogWarning(ex,
                    "[PersonalVideoService] Concurrent trigger for ProgramId={ProgramId}, StudentId={StudentId}; returning existing record.",
                    programId, studentId);

                var winner = await _unitOfWork.HighlightVideos.FirstOrDefaultAsync(
                    hv => hv.ProgramId == programId && hv.StudentId == studentId && !hv.IsDeleted);

                if (winner != null)
                    return MapToDto(winner);

                throw;
            }
        }
        else
        {
            // Regenerate: keep the previous VideoUrl so the old video stays available until the
            // new job completes; reset the stale job ref / failure reason.
            existing.PersonalVideoStatus = HighlightVideoStatus.Processing;
            existing.PersonalVideoRequestedAt = DateTime.UtcNow;
            existing.PersonalVideoJobRef = null;
            existing.PersonalVideoFailureReason = null;
            await _unitOfWork.SaveChangesAsync();
        }

        // ── 4. Hand off the heavy work (clip build + MediaConvert) to the worker ──
        _queue.Enqueue(new PersonalVideoJob(existing.Id, programId, studentId, strengthDescription));

        _logger.LogInformation(
            "[PersonalVideoService] Generation queued. HighlightVideoId={Id}, ProgramId={ProgramId}, StudentId={StudentId}",
            existing.Id, programId, studentId);

        return MapToDto(existing);
    }

    /// <inheritdoc />
    public async Task ProcessGenerationAsync(PersonalVideoJob job)
    {
        var record = await _unitOfWork.HighlightVideos.FirstOrDefaultAsync(
            hv => hv.Id == job.HighlightVideoId && !hv.IsDeleted);

        if (record == null)
        {
            _logger.LogWarning(
                "[PersonalVideoService] ProcessGenerationAsync: HighlightVideo {Id} not found; skipping.",
                job.HighlightVideoId);
            return;
        }

        try
        {
            // ── Collect all tagged, fully-processed videos in this Program ──
            var clipInputs = await BuildClipInputsAsync(
                job.ProgramId, job.StudentId, job.StrengthDescription);

            if (clipInputs.Count == 0)
            {
                var reason = !string.IsNullOrWhiteSpace(job.StrengthDescription)
                    ? $"No video segments matched the specified strengths: '{job.StrengthDescription}'. " +
                      "Ensure the student's strengths are visible in the tagged videos and label detection has completed."
                    : "No processed video assets tagged for this student were found in the program.";

                _logger.LogInformation(
                    "[PersonalVideoService] No clips for HighlightVideoId={Id}: {Reason}", record.Id, reason);

                record.PersonalVideoStatus = HighlightVideoStatus.Failed;
                record.PersonalVideoFailureReason = reason;
                await _unitOfWork.SaveChangesAsync();
                return;
            }

            // ── Submit MediaConvert job ──
            var outputKey = $"{PersonalVideoFolder}/{job.StudentId}_{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}.mp4";

            _logger.LogInformation(
                "[PersonalVideoService] Submitting personal video job: {ClipCount} clip(s) → {OutputKey}",
                clipInputs.Count, outputKey);

            var mcJobId = await _videoConverterService.SubmitPersonalVideoJobAsync(clipInputs, outputKey);

            record.PersonalVideoJobRef = mcJobId;
            record.PersonalVideoStatus = HighlightVideoStatus.Processing;
            record.PersonalVideoFailureReason = null;
            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation(
                "[PersonalVideoService] Job submitted. HighlightVideoId={Id}, McJobId={McJobId}. Awaiting MediaConvert webhook.",
                record.Id, mcJobId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "[PersonalVideoService] Generation failed for HighlightVideoId={Id}.", record.Id);

            record.PersonalVideoStatus = HighlightVideoStatus.Failed;
            record.PersonalVideoFailureReason =
                "An internal error occurred during video generation. Please try again later.";
            try
            {
                await _unitOfWork.SaveChangesAsync();
            }
            catch (Exception saveEx)
            {
                _logger.LogError(saveEx,
                    "[PersonalVideoService] Failed to persist Failed status for HighlightVideoId={Id}.", record.Id);
            }
        }
    }

    /// <inheritdoc />
    public async Task<HighlightVideoDto?> GetHighlightVideoAsync(Guid programId, Guid studentId)
    {
        var record = await _unitOfWork.HighlightVideos.FirstOrDefaultAsync(
            hv => hv.ProgramId == programId && hv.StudentId == studentId && !hv.IsDeleted);

        return record == null ? null : MapToDto(record);
    }

    /// <inheritdoc />
    public async Task HandlePersonalVideoJobCompletionAsync(string jobId, bool isSuccess)
    {
        _logger.LogInformation(
            "[PersonalVideoService] Handling MediaConvert Webhook JobId: {JobId}, Success: {Success}", jobId, isSuccess);

        var highlightVideo = await _unitOfWork.HighlightVideos.FirstOrDefaultAsync(
            h => h.PersonalVideoJobRef == jobId && !h.IsDeleted);

        if (highlightVideo == null)
            return;

        if (isSuccess)
        {
            try
            {
                var outputS3Key = await _videoConverterService.GetOutputS3KeyAsync(jobId);
                var videoUrl = await _blobService.GetPreviewUrlAsync(outputS3Key);

                highlightVideo.VideoUrl = videoUrl;
                highlightVideo.PersonalVideoStatus = HighlightVideoStatus.Completed;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "[PersonalVideoService] Failed to resolve MediaConvert output for HighlightVideo {Id}", highlightVideo.Id);
                highlightVideo.PersonalVideoStatus = HighlightVideoStatus.Failed;
            }
        }
        else
        {
            highlightVideo.PersonalVideoStatus = HighlightVideoStatus.Failed;
        }

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
    /// When <paramref name="strengthDescription"/> is provided, each video's face segments are
    /// additionally filtered via Claude (Bedrock) against the label detection timeline.
    /// </summary>
    private async Task<List<ClipInput>> BuildClipInputsAsync(
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

            var clip = await BuildClipInputForMediaAsync(media, s3Key, studentId, strengthDescription);
            if (clip != null)
                clips.Add(clip);
        }

        _logger.LogInformation(
            "[PersonalVideoService] BuildClipInputsAsync completed: {Count} ClipInput(s) built.",
            clips.Count);

        return clips;
    }

    /// <summary>
    /// Applies Logic Core rules to a single <see cref="MediaAsset"/> and returns the
    /// corresponding <see cref="ClipInput"/> (with or without <see cref="TimeClip"/>s).
    /// When <paramref name="strengthDescription"/> is provided, face segments are cross-referenced
    /// against the Label Detection timeline via Claude (Bedrock) before being used as clips.
    /// Returns <c>null</c> when strengths filtering yields no matched segments for this video
    /// (the video is simply skipped — other videos in the program may still contribute).
    /// </summary>
    private async Task<ClipInput?> BuildClipInputForMediaAsync(
        MediaAsset media, string s3Key, Guid studentId, string? strengthDescription = null)
    {
        // Read the face timeline captured at tagging time (no Rekognition re-query — its
        // results expire after 7 days). See MediaTag.FaceSegmentsJson.
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
                return null;
            }

            _logger.LogWarning(
                "[PersonalVideoService] No persisted timeline; sole tagged student → full video. MediaId={MediaId}",
                media.Id);
            return new ClipInput(s3Key, new List<TimeClip>());
        }

        // ── Case 1: No student face detected (AI fallback / scene-only) ─────────
        if (timelineResult.Segments.Count == 0)
        {
            if (!string.IsNullOrWhiteSpace(strengthDescription))
            {
                var fullVideoSegment = new List<FaceTimestampSegment>
                    { new FaceTimestampSegment(0, FullVideoEndMs) };

                var filteredClips = await ApplyStrengthsFilterAsync(
                    media, s3Key, fullVideoSegment, strengthDescription);

                if (filteredClips != null)
                {
                    if (filteredClips.Clips.Count > 0)
                    {
                        // Strength content present → full video (ignore Claude's sub-clip timings)
                        _logger.LogInformation(
                            "[PersonalVideoService] Case 1 + strengths: strength labels found → full video. MediaId={MediaId}", media.Id);
                        return new ClipInput(s3Key, new List<TimeClip>());
                    }

                    // No matching strength labels → skip video entirely
                    _logger.LogInformation(
                        "[PersonalVideoService] Case 1 + strengths: no matching strength labels → skipping. MediaId={MediaId}", media.Id);
                    return null;
                }

                // null → label data unavailable → fallback to full video
                _logger.LogWarning(
                    "[PersonalVideoService] Case 1 + strengths: label data unavailable → full video fallback. MediaId={MediaId}", media.Id);
            }
            else
            {
                _logger.LogInformation(
                    "[PersonalVideoService] Case 1 (no student face detected by AI) → full video. MediaId={MediaId}", media.Id);
            }

            return new ClipInput(s3Key, new List<TimeClip>());
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
                var filteredClips = await ApplyStrengthsFilterAsync(media, s3Key, timelineResult.Segments, strengthDescription);
                if (filteredClips != null)
                    return filteredClips.Clips.Count == 0 ? null : filteredClips;

                _logger.LogWarning(
                    "[PersonalVideoService] Case 2 strengths filter fallback → full video for MediaId={MediaId}", media.Id);
            }

            return new ClipInput(s3Key, new List<TimeClip>());
        }

        // ── Case 3 & 4: Multiple people — extract student's timeline ──────────────
        _logger.LogInformation(
            "[PersonalVideoService] Case 3 & 4 (mixed faces per persisted timeline) → extracting timeline. MediaId={MediaId}", media.Id);

        var faceSegments = timelineResult.Segments;

        // ── Strengths Filtering (optional) ────────────────────────────────────────
        if (!string.IsNullOrWhiteSpace(strengthDescription))
        {
            var filteredClips = await ApplyStrengthsFilterAsync(media, s3Key, faceSegments, strengthDescription);
            // null  → label data unavailable, fall back to face-only for this video.
            // empty → strengths checked but nothing matched, skip this video entirely.
            if (filteredClips != null)
                return filteredClips.Clips.Count == 0 ? null : filteredClips;

            _logger.LogWarning(
                "[PersonalVideoService] Strengths filter fallback → face-only for MediaId={MediaId}", media.Id);
        }

        // Standard face-timeline path (no strengths OR fallback from strengths filter)
        var timeClips = MergeAndFormatTimeClips(faceSegments.Select(s => new MatchedSegment(s.StartMs, s.EndMs, "", 0)));

        _logger.LogInformation(
            "[PersonalVideoService] Case 3/4: {Raw} raw → {Merged} merged segment(s) for MediaId={MediaId}",
            faceSegments.Count, timeClips.Count, media.Id);

        return new ClipInput(s3Key, timeClips);
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
    /// Loads the Label Detection timeline for <paramref name="media"/> then calls
    /// <see cref="IStrengthMatchService.MatchStrengthsAsync"/> to obtain strength-filtered clips.
    /// </summary>
    /// <returns>
    /// A <see cref="ClipInput"/> whose <c>Clips</c> may be empty (no match) or populated (matched).
    /// Returns <c>null</c> when label data is unavailable — caller should fall back to face-only.
    /// </returns>
    private async Task<ClipInput?> ApplyStrengthsFilterAsync(
        MediaAsset media, string s3Key,
        IList<FaceTimestampSegment> faceSegments,
        string strengthDescription)
    {
        // Read the label timeline captured at label-detection time (no Rekognition re-query —
        // its results expire after 7 days). See MediaAsset.LabelTimelineJson.
        if (string.IsNullOrEmpty(media.LabelTimelineJson))
        {
            _logger.LogWarning(
                "[PersonalVideoService] No persisted label timeline for MediaId={MediaId}. " +
                "Label Detection not finished, failed, or capture failed. Falling back to face-only.",
                media.Id);
            return null;
        }

        List<LabelDetectionEntry>? labelTimeline;
        try
        {
            labelTimeline = JsonSerializer.Deserialize<List<LabelDetectionEntry>>(media.LabelTimelineJson);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "[PersonalVideoService] Failed to deserialize LabelTimelineJson for MediaId={MediaId}. Falling back.",
                media.Id);
            return null;
        }

        if (labelTimeline == null || labelTimeline.Count == 0)
        {
            _logger.LogWarning(
                "[PersonalVideoService] Persisted label timeline is empty for MediaId={MediaId}. Skipping video.",
                media.Id);
            return new ClipInput(s3Key, new List<TimeClip>()); // empty → skipped by caller
        }

        // ── Token optimisation ────────────────────────────────────────────────
        // Only send labels that fall within each face segment ± LabelContextWindowMs.
        // This can reduce the label count by 80-95 % for long videos, staying well
        // within Bedrock's daily token quota while keeping all relevant context.
        var relevantLabels = FilterLabelsToSegmentWindows(labelTimeline, faceSegments);

        if (relevantLabels.Count == 0)
        {
            // No labels overlap the student's face windows at all → no match possible.
            _logger.LogInformation(
                "[PersonalVideoService] No labels found within face-segment windows for MediaId={MediaId}. Skipping video.",
                media.Id);
            return new ClipInput(s3Key, new List<TimeClip>());
        }

        _logger.LogInformation(
            "[PersonalVideoService] Label timeline filtered: {Total} → {Filtered} entries for MediaId={MediaId}",
            labelTimeline.Count, relevantLabels.Count, media.Id);

        // Cross-reference via Claude (Bedrock Converse API + Tool Use)
        StrengthMatchResult matchResult;
        try
        {
            matchResult = await _strengthMatchService.MatchStrengthsAsync(faceSegments, relevantLabels, strengthDescription);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "[PersonalVideoService] Claude strength matching failed for MediaId={MediaId}. Falling back.",
                media.Id);
            return null;
        }

        if (matchResult.MatchedSegments.Count == 0)
        {
            _logger.LogInformation(
                "[PersonalVideoService] No strength matches for MediaId={MediaId}. Reasoning: {Reasoning}",
                media.Id, matchResult.Reasoning);
            return new ClipInput(s3Key, new List<TimeClip>()); // empty → skipped by caller
        }

        // Convert to TimeClips using the foolproof merge logic
        var timeClips = MergeAndFormatTimeClips(matchResult.MatchedSegments);

        _logger.LogInformation(
            "[PersonalVideoService] Strengths filter: {Count} clip(s) for MediaId={MediaId}. Reasoning: {Reasoning}",
            timeClips.Count, media.Id, matchResult.Reasoning);

        return new ClipInput(s3Key, timeClips);
    }

    /// <summary>
    /// Converts a list of raw segments into AWS MediaConvert-compatible TimeClips, mathematically guaranteeing 
    /// strictly ascending and non-overlapping StartTimecodes to prevent ERROR 1040.
    /// </summary>
    private static List<TimeClip> MergeAndFormatTimeClips(IEnumerable<MatchedSegment> segments)
    {
        // 1. Convert to TimeClips.
        //    IMPORTANT: face timelines are collapsed with a 500 ms gap while Rekognition samples
        //    faces ~once per second, so MOST face segments are single-sample points where
        //    StartMs == EndMs. We must keep those (StartMs <= EndMs) and only drop genuine
        //    hallucinations where StartMs > EndMs. The buffer is applied AFTER this check so a
        //    point [t, t] becomes a valid [t-BufferMs, t+BufferMs] clip — dropping points here
        //    (the old `StartMs < EndMs`) emptied the clip list and fell back to the full video.
        var timeClipsRaw = segments
            .Where(s => s.StartMs <= s.EndMs) // keep point detections; drop EndMs < StartMs hallucinations
            // Apply BufferMs padding on both sides (clamped to 0) for breathing room around each segment.
            .Select(s => new { Start = Math.Max(0, s.StartMs - BufferMs), End = s.EndMs + BufferMs })
            .Select(seg => new TimeClip(MsToTimecode(seg.Start), MsToTimecode(seg.End)))
            .Where(t => t.StartTimecode != t.EndTimecode) // drop 0-duration clips
            .OrderBy(t => t.StartTimecode)
            .ToList();

        // 2. Merge overlapping or identical StartTimecodes
        var timeClips = new List<TimeClip>();
        foreach (var tc in timeClipsRaw)
        {
            if (timeClips.Count == 0)
            {
                timeClips.Add(tc);
                continue;
            }

            var last = timeClips[^1];
            // If the start timecode is equal to or earlier than the previous one's start
            if (string.Compare(tc.StartTimecode, last.StartTimecode) <= 0)
            {
                // Merge them by extending the EndTimecode of the last clip if needed
                if (string.Compare(tc.EndTimecode, last.EndTimecode) > 0)
                {
                    timeClips[^1] = last with { EndTimecode = tc.EndTimecode };
                }
            }
            // If it overlaps with the previous clip's end time
            else if (string.Compare(tc.StartTimecode, last.EndTimecode) <= 0)
            {
                // Extend end time
                if (string.Compare(tc.EndTimecode, last.EndTimecode) > 0)
                {
                    timeClips[^1] = last with { EndTimecode = tc.EndTimecode };
                }
            }
            else
            {
                timeClips.Add(tc);
            }
        }
        return timeClips;
    }


    // ─────────────────────────────────────────────────────────────────────────
    // Segment processing helpers
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Filters <paramref name="allLabels"/> to only those entries whose timestamp
    /// falls within any face segment ± <see cref="LabelContextWindowMs"/>.
    /// This reduces the prompt token count by 80-95 % on long videos while preserving
    /// all contextually relevant label data for Claude to reason about.
    /// </summary>
    private static List<LabelDetectionEntry> FilterLabelsToSegmentWindows(
        IList<LabelDetectionEntry> allLabels,
        IList<FaceTimestampSegment> faceSegments)
    {
        // Build merged windows with context padding to avoid repeated overlap checks.
        var windows = faceSegments
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

    private static HighlightVideoDto MapToDto(HighlightVideo hv) => new()
    {
        Id = hv.Id,
        StudentId = hv.StudentId,
        ProgramId = hv.ProgramId,
        VideoUrl = hv.VideoUrl,
        PersonalVideoStatus = hv.PersonalVideoStatus,
        PersonalVideoRequestedAt = hv.PersonalVideoRequestedAt,
        PersonalVideoFailureReason = hv.PersonalVideoFailureReason,
    };
}
