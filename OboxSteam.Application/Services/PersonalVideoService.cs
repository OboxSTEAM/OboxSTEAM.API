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
/// Logic Core (clipping rules per source video):
///   Case 1 — Video has ZERO faces detected by Rekognition (scene-only / activity footage)
///             but was tagged for the student → include the ENTIRE video (Fallback).
///   Case 2 — Video's only detected face belongs to the target student → include ENTIRE video.
///   Case 3 — Video has multiple people → extract only the student's segments from the timeline
///             using 3-second buffers and merging gaps shorter than 3 seconds.
///   Fallback — Timeline extraction returned no segments for the student
///              (AI could not pinpoint the face) → include ENTIRE video.
///
/// Strengths Filtering (optional — triggered when caller supplies a strength description):
///   After building face segments, cross-references them with the Rekognition Label Detection
///   timeline via Claude (Bedrock). Only segments where the student is performing a matched
///   strength are kept. If no match is found across all videos a BadRequest is thrown.
/// </summary>
public class PersonalVideoService : IPersonalVideoService
{
    // ── Clipping constants ───────────────────────────────────────────────────
    /// <summary>Milliseconds of padding added before/after each detected face segment.</summary>
    private const long BufferMs = 2_000;

    /// <summary>
    /// Adjacent segments whose gap is shorter than this are merged into one.
    /// Keeps the clip continuous when the student briefly leaves and re-enters frame.
    /// </summary>
    private const long MergeGapMs = 1_000;

    private const string PersonalVideoFolder = "personal-videos";

    private readonly string _s3Bucket;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IFaceRecognitionService _faceRecognitionService;
    private readonly IVideoConverterService _videoConverterService;
    private readonly IStrengthMatchService _strengthMatchService;
    private readonly IBlobService _blobService;
    private readonly ILogger<PersonalVideoService> _logger;

    public PersonalVideoService(
        IUnitOfWork unitOfWork,
        IFaceRecognitionService faceRecognitionService,
        IVideoConverterService videoConverterService,
        IStrengthMatchService strengthMatchService,
        IBlobService blobService,
        ILogger<PersonalVideoService> logger)
    {
        _unitOfWork = unitOfWork;
        _faceRecognitionService = faceRecognitionService;
        _videoConverterService = videoConverterService;
        _strengthMatchService = strengthMatchService;
        _blobService = blobService;
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
            "[PersonalVideoService] TriggerPersonalVideoGenerationAsync: ProgramId={ProgramId}, StudentId={StudentId}",
            programId, studentId);

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
            _logger.LogInformation(
                "[PersonalVideoService] Job already in progress for ProgramId={ProgramId}, StudentId={StudentId}",
                programId, studentId);
            return MapToDto(existing, 0);
        }

        // ── 3. Collect all tagged, fully-processed videos in this Program ────
        var clipInputs = await BuildClipInputsAsync(programId, studentId, strengthDescription);

        if (clipInputs.Count == 0)
        {
            var reason = !string.IsNullOrWhiteSpace(strengthDescription)
                ? $"No video segments matched the specified strengths: '{strengthDescription}'. " +
                  "Ensure the student's strengths are visible in the tagged videos and label detection has completed."
                : "No processed video assets tagged for this student were found in the program.";
            throw ErrorHelper.BadRequest(reason);
        }

        // ── 4. Submit MediaConvert job ───────────────────────────────────────
        var outputKey = $"{PersonalVideoFolder}/{studentId}_{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}.mp4";

        _logger.LogInformation(
            "[PersonalVideoService] Submitting personal video job: {ClipCount} clip(s) → {OutputKey}",
            clipInputs.Count, outputKey);

        string mcJobId;
        try
        {
            mcJobId = await _videoConverterService.SubmitPersonalVideoJobAsync(clipInputs, outputKey);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[PersonalVideoService] Failed to submit MediaConvert job.");
            throw ErrorHelper.Internal("Failed to start video generation. Please try again later.");
        }

        // ── 5. Persist / upsert HighlightVideo record ────────────────────────
        if (existing == null)
        {
            existing = new HighlightVideo
            {
                StudentId = studentId,
                ProgramId = programId,
            };
            await _unitOfWork.HighlightVideos.AddAsync(existing);
        }

        existing.VideoUrl = null; // will be set when job completes
        existing.PersonalVideoJobRef = mcJobId;
        existing.PersonalVideoStatus = HighlightVideoStatus.Processing;
        existing.PersonalVideoRequestedAt = DateTime.UtcNow;
        // Note: do NOT set existing.Status — it is obsolete. Use PersonalVideoStatus only.

        await _unitOfWork.SaveChangesAsync();

        // ── 6. Wait for AWS Webhook to notify completion ─────────────────────
        _logger.LogInformation(
            "[PersonalVideoService] Job submitted. HighlightVideoId={Id}, McJobId={McJobId}",
            existing.Id, mcJobId);

        return MapToDto(existing, clipInputs.Count);
    }

    /// <inheritdoc />
    public async Task<HighlightVideoDto?> GetHighlightVideoAsync(Guid programId, Guid studentId)
    {
        var record = await _unitOfWork.HighlightVideos.FirstOrDefaultAsync(
            hv => hv.ProgramId == programId && hv.StudentId == studentId && !hv.IsDeleted);

        return record == null ? null : MapToDto(record, 0);
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

        // Build ClipInputs in parallel — each Case 3 video requires a Rekognition API call.
        // Running concurrently avoids sequential latency when many videos are tagged.
        var clipTasks = taggedMedia.Select(async media =>
        {
            var s3Key = ExtractS3KeyFromUrl(media.FileUrl);
            if (string.IsNullOrEmpty(s3Key))
            {
                _logger.LogWarning(
                    "[PersonalVideoService] Cannot extract S3 key from FileUrl for MediaId={MediaId}. Skipping.",
                    media.Id);
                return null;
            }

            return (ClipInput?)await BuildClipInputForMediaAsync(media, s3Key, studentId, strengthDescription);
        });

        var clipResults = await Task.WhenAll(clipTasks);
        var clips = clipResults.Where(c => c != null).Cast<ClipInput>().ToList();

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
        if (string.IsNullOrEmpty(media.FaceSearchJobId))
        {
            _logger.LogWarning(
                "[PersonalVideoService] No Rekognition FaceSearchJobId for MediaId={MediaId}. Using full video (Fallback).",
                media.Id);
            return new ClipInput(s3Key, new List<TimeClip>());
        }

        VideoFaceTimelineResult? timelineResult;
        try
        {
            timelineResult = await _faceRecognitionService
                .GetVideoFaceTimelineAsync(media.FaceSearchJobId, studentId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "[PersonalVideoService] GetVideoFaceTimelineAsync failed for MediaId={MediaId}. Using full video (Fallback).",
                media.Id);
            return new ClipInput(s3Key, new List<TimeClip>());
        }

        if (timelineResult == null)
        {
            _logger.LogInformation(
                "[PersonalVideoService] Job IN_PROGRESS or FAILED -> fallback to full video. MediaId={MediaId}", media.Id);
            return new ClipInput(s3Key, new List<TimeClip>());
        }

        // ── Case 1: No student face detected (AI fallback / scene-only) ─────────
        if (timelineResult.Segments.Count == 0)
        {
            _logger.LogInformation(
                "[PersonalVideoService] Case 1 (no student face detected by AI) → full video. MediaId={MediaId}", media.Id);
            return new ClipInput(s3Key, new List<TimeClip>());
        }

        // ── Case 2: Only this student's face in the video (AI confirmed) ────────────
        if (!timelineResult.HasOtherFaces)
        {
            _logger.LogInformation(
                "[PersonalVideoService] Case 2 (sole face confirmed by AI). MediaId={MediaId}", media.Id);

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
            "[PersonalVideoService] Case 3 & 4 (mixed faces confirmed by AI) → extracting timeline. MediaId={MediaId}", media.Id);

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
        var mergedSegments = ApplyBufferAndMerge(faceSegments);
        var timeClips = mergedSegments.Select(s => new TimeClip(
            MsToTimecode(s.StartMs),
            MsToTimecode(s.EndMs))).ToList();

        _logger.LogInformation(
            "[PersonalVideoService] Case 3/4: {Raw} raw → {Merged} merged segment(s) for MediaId={MediaId}",
            faceSegments.Count, mergedSegments.Count, media.Id);

        return new ClipInput(s3Key, timeClips);
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
        if (string.IsNullOrEmpty(media.LabelJobRef))
        {
            _logger.LogWarning(
                "[PersonalVideoService] LabelJobRef is null for MediaId={MediaId}. " +
                "Label Detection was not triggered or video is still processing. Falling back to face-only.",
                media.Id);
            return null;
        }

        List<LabelDetectionEntry>? labelTimeline;
        try
        {
            labelTimeline = await _faceRecognitionService
                .GetLabelDetectionResultsAsync(media.LabelJobRef);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "[PersonalVideoService] GetLabelDetectionResultsAsync failed for MediaId={MediaId}. Falling back.",
                media.Id);
            return null;
        }

        if (labelTimeline == null)
        {
            // IN_PROGRESS — label job not done yet
            _logger.LogWarning(
                "[PersonalVideoService] Label Detection still IN_PROGRESS for MediaId={MediaId}. Falling back.",
                media.Id);
            return null;
        }

        if (labelTimeline.Count == 0)
        {
            _logger.LogWarning(
                "[PersonalVideoService] Label Detection returned 0 labels for MediaId={MediaId}. Skipping video.",
                media.Id);
            return new ClipInput(s3Key, new List<TimeClip>()); // empty → skipped by caller
        }

        // Cross-reference via Claude (Bedrock Converse API + Tool Use)
        StrengthMatchResult matchResult;
        try
        {
            matchResult = await _strengthMatchService.MatchStrengthsAsync(faceSegments, labelTimeline, strengthDescription);
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

        // MatchedSegments are already sorted by score desc from BedrockStrengthMatchService
        var timeClips = matchResult.MatchedSegments
            .Select(seg => new TimeClip(MsToTimecode(seg.StartMs), MsToTimecode(seg.EndMs)))
            .ToList();

        _logger.LogInformation(
            "[PersonalVideoService] Strengths filter: {Count} clip(s) for MediaId={MediaId}. Reasoning: {Reasoning}",
            timeClips.Count, media.Id, matchResult.Reasoning);

        return new ClipInput(s3Key, timeClips);
    }


    // ─────────────────────────────────────────────────────────────────────────
    // Segment processing helpers
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Applies a <see cref="BufferMs"/>-millisecond padding to each segment (clamped to 0),
    /// then merges any overlapping or near-adjacent segments (gap &lt; <see cref="MergeGapMs"/>).
    /// </summary>
    private static List<FaceTimestampSegment> ApplyBufferAndMerge(
        IEnumerable<FaceTimestampSegment> segments)
    {
        // 1. Apply buffer (clamp start to 0)
        var buffered = segments
            .Select(s => new FaceTimestampSegment(
                Math.Max(0, s.StartMs - BufferMs),
                s.EndMs + BufferMs))
            .OrderBy(s => s.StartMs)
            .ToList();

        // 2. Merge overlapping / near-adjacent
        var merged = new List<FaceTimestampSegment>();
        foreach (var seg in buffered)
        {
            if (merged.Count == 0)
            {
                merged.Add(seg);
                continue;
            }

            var last = merged[^1];
            // Gap between last.EndMs and seg.StartMs
            if (seg.StartMs - last.EndMs <= MergeGapMs)
            {
                // Extend the last segment
                merged[^1] = last with { EndMs = Math.Max(last.EndMs, seg.EndMs) };
            }
            else
            {
                merged.Add(seg);
            }
        }

        return merged;
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

    private static HighlightVideoDto MapToDto(HighlightVideo hv, int sourceCount) => new()
    {
        Id = hv.Id,
        StudentId = hv.StudentId,
        ProgramId = hv.ProgramId,
        VideoUrl = hv.VideoUrl,
        PersonalVideoStatus = hv.PersonalVideoStatus,
        PersonalVideoRequestedAt = hv.PersonalVideoRequestedAt,
        SourceVideoCount = sourceCount,
    };
}
