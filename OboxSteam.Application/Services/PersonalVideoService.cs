using Microsoft.Extensions.Logging;
using OboxSteam.Application.Commons;
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
/// </summary>
public class PersonalVideoService : IPersonalVideoService
{
    // ── Clipping constants ───────────────────────────────────────────────────
    /// <summary>Seconds of padding added before/after each detected segment.</summary>
    private const long BufferMs = 2_000;

    /// <summary>Adjacent segments whose gap is shorter than this are merged.</summary>
    private const long MergeGapMs = 1_000;

    private const string PersonalVideoFolder = "personal-videos";
    private const string S3Bucket = "oboxsteam-bucket";

    private readonly IUnitOfWork _unitOfWork;
    private readonly IFaceRecognitionService _faceRecognitionService;
    private readonly IVideoConverterService _videoConverterService;
    private readonly IBlobService _blobService;
    private readonly PersonalVideoChannel _channel;
    private readonly ILogger<PersonalVideoService> _logger;

    public PersonalVideoService(
        IUnitOfWork unitOfWork,
        IFaceRecognitionService faceRecognitionService,
        IVideoConverterService videoConverterService,
        IBlobService blobService,
        PersonalVideoChannel channel,
        ILogger<PersonalVideoService> logger)
    {
        _unitOfWork = unitOfWork;
        _faceRecognitionService = faceRecognitionService;
        _videoConverterService = videoConverterService;
        _blobService = blobService;
        _channel = channel;
        _logger = logger;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Public API
    // ─────────────────────────────────────────────────────────────────────────

    /// <inheritdoc />
    public async Task<HighlightVideoDto> TriggerPersonalVideoGenerationAsync(Guid programId, Guid studentId)
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
        var clipInputs = await BuildClipInputsAsync(programId, studentId);

        if (clipInputs.Count == 0)
            throw ErrorHelper.BadRequest(
                "No processed video assets tagged for this student were found in the program.");

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
        existing.Status = "Processing";

        await _unitOfWork.SaveChangesAsync();

        // ── 6. Enqueue for background polling ────────────────────────────────
        await _channel.Writer.WriteAsync(existing.Id);

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
    /// </summary>
    private async Task<List<ClipInput>> BuildClipInputsAsync(Guid programId, Guid studentId)
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

            var clipInput = await BuildClipInputForMediaAsync(media, s3Key, studentId);
            clips.Add(clipInput);
        }

        _logger.LogInformation(
            "[PersonalVideoService] BuildClipInputsAsync completed: {Count} ClipInput(s) built.",
            clips.Count);

        return clips;
    }

    /// <summary>
    /// Applies Logic Core rules to a single <see cref="MediaAsset"/> and returns the
    /// corresponding <see cref="ClipInput"/> (with or without <see cref="TimeClip"/>s).
    /// </summary>
    private async Task<ClipInput> BuildClipInputForMediaAsync(
        MediaAsset media, string s3Key, Guid studentId)
    {
        var otherFaces = media.MediaTags
            .Where(t => t.StudentId != studentId)
            .ToList();

        // ── Case 1: No faces detected at all (scene-only / Fallback) ─────────
        // A video with zero MediaTags has scene-only content. Since it was manually
        // tagged for this student, include the whole video.
        if (!media.MediaTags.Any())
        {
            _logger.LogInformation(
                "[PersonalVideoService] Case 1 (no faces) → full video. MediaId={MediaId}", media.Id);
            return new ClipInput(s3Key, new List<TimeClip>());
        }

        // ── Case 2: Only this student's face in the video ────────────────────
        if (!otherFaces.Any())
        {
            _logger.LogInformation(
                "[PersonalVideoService] Case 2 (sole face) → full video. MediaId={MediaId}", media.Id);
            return new ClipInput(s3Key, new List<TimeClip>());
        }

        // ── Case 3: Multiple people — extract student's timeline ──────────────
        _logger.LogInformation(
            "[PersonalVideoService] Case 3 (mixed faces) → extracting timeline. MediaId={MediaId}", media.Id);

        if (string.IsNullOrEmpty(media.VideoJobRef))
        {
            _logger.LogWarning(
                "[PersonalVideoService] No Rekognition JobRef for MediaId={MediaId}. Using full video (Fallback).",
                media.Id);
            return new ClipInput(s3Key, new List<TimeClip>());
        }

        List<FaceTimestampSegment> rawSegments;
        try
        {
            rawSegments = await _faceRecognitionService
                .GetVideoFaceTimelineAsync(media.VideoJobRef, studentId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "[PersonalVideoService] GetVideoFaceTimelineAsync failed for MediaId={MediaId}. Using full video (Fallback).",
                media.Id);
            return new ClipInput(s3Key, new List<TimeClip>());
        }

        // Fallback: AI could not pinpoint the student's face
        if (rawSegments.Count == 0)
        {
            _logger.LogInformation(
                "[PersonalVideoService] No segments found (AI fallback) → full video. MediaId={MediaId}", media.Id);
            return new ClipInput(s3Key, new List<TimeClip>());
        }

        var mergedSegments = ApplyBufferAndMerge(rawSegments);
        var timeClips = mergedSegments.Select(s => new TimeClip(
            MsToTimecode(s.StartMs),
            MsToTimecode(s.EndMs))).ToList();

        _logger.LogInformation(
            "[PersonalVideoService] Case 3: {Raw} raw → {Merged} merged segment(s) for MediaId={MediaId}",
            rawSegments.Count, mergedSegments.Count, media.Id);

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
    /// Converts milliseconds to AWS MediaConvert InputClipping timecode format: <c>HH:MM:SS:FF</c>
    /// where FF is the zero-based frame number (2 digits, 00–29 at 30 fps).
    ///
    /// AWS MediaConvert requires exactly 2 digits for the frame field:
    ///   pattern = ^([01][0-9]|2[0-4]):[0-5][0-9]:[0-5][0-9][:;][0-9]{2}$
    ///
    /// We assume 30 fps which matches the H.264 output preset used by VideoConverterService.
    /// </summary>
    private static string MsToTimecode(long totalMs, int fps = 30)
    {
        // Convert ms → total frames, then decompose into HH:MM:SS:FF
        var totalFrames = (long)Math.Round(totalMs * fps / 1000.0);
        var frames = (int)(totalFrames % fps);
        var totalSec = totalFrames / fps;
        var sec = (int)(totalSec % 60);
        var min = (int)(totalSec / 60 % 60);
        var hr = (int)(totalSec / 3_600);
        return $"{hr:D2}:{min:D2}:{sec:D2}:{frames:D2}";
    }

    /// <summary>
    /// Extracts the S3 bucket-relative key from a presigned or public URL.
    /// Falls back to treating the raw FileUrl as a key if parsing fails.
    /// </summary>
    private static string? ExtractS3KeyFromUrl(string? fileUrl)
    {
        if (string.IsNullOrWhiteSpace(fileUrl)) return null;

        try
        {
            var uri = new Uri(fileUrl);
            // Presigned URLs: path = /{bucket}/{key}  OR  /{key} depending on style
            // We strip the leading '/' and any bucket prefix.
            var path = uri.AbsolutePath.TrimStart('/');

            // If the path starts with the bucket name, strip it
            if (path.StartsWith(S3Bucket + "/", StringComparison.OrdinalIgnoreCase))
                path = path[(S3Bucket.Length + 1)..];

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
