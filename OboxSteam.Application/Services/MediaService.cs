using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using OboxSteam.Application.DTOs.MediaDTO;
using OboxSteam.Application.Interfaces;
using OboxSteam.Application.Utils;
using OboxSteam.Domain.Entities;
using OboxSteam.Domain.Enums;
using OboxSteam.Domain.Interfaces;

namespace OboxSteam.Application.Services;

public class MediaService : IMediaService
{
    private static readonly HashSet<string> ImageExtensions = new(StringComparer.OrdinalIgnoreCase)
        { ".jpg", ".jpeg", ".png" };

    private static readonly HashSet<string> VideoExtensions = new(StringComparer.OrdinalIgnoreCase)
        { ".mp4", ".mov" };

    private const long MaxImageSize = 10 * 1024 * 1024;  // 10 MB
    private const long MaxVideoSize = 3L * 1024 * 1024 * 1024;  // 3 GB
    private const string MediaFolder = "media";
    private const string RawFolder = "raw";



    private readonly IClaimsService _claimsService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IBlobService _blobService;
    private readonly IFaceRecognitionService _faceRecognitionService;
    private readonly ILogger<MediaService> _logger;
    private readonly IVideoConverterService _videoConverterService;

    public MediaService(
        IClaimsService claimsService,
        IUnitOfWork unitOfWork,
        IBlobService blobService,
        IFaceRecognitionService faceRecognitionService,
        ILogger<MediaService> logger,
        IVideoConverterService videoConverterService)
    {
        _claimsService = claimsService;
        _unitOfWork = unitOfWork;
        _blobService = blobService;
        _faceRecognitionService = faceRecognitionService;
        _logger = logger;
        _videoConverterService = videoConverterService;
    }

    /// <inheritdoc />
    public async Task<MediaAssetDto> UploadMediaAsync(IFormFile file, Guid activityId)
    {
        var userId = _claimsService.GetCurrentUserId;
        _logger.LogInformation("UploadMediaAsync started by UserId: {UserId} for Activity: {ActivityId}", userId, activityId);

        // ── Validate ─────────────────────────────────────────────────────────
        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        var isImage = ImageExtensions.Contains(extension);
        var isVideo = VideoExtensions.Contains(extension);

        if (!isImage && !isVideo)
            throw ErrorHelper.BadRequest("Only image (.jpg, .jpeg, .png) and video (.mp4, .mov) files are allowed.");

        if (isImage && file.Length > MaxImageSize)
            throw ErrorHelper.BadRequest("Image file size must not exceed 10 MB.");

        if (isVideo && file.Length > MaxVideoSize)
            throw ErrorHelper.BadRequest("Video file size must not exceed 3 GB.");

        // Verify activity exists
        var activity = await _unitOfWork.Activities.GetByIdAsync(activityId);
        if (activity == null || activity.IsDeleted)
            throw ErrorHelper.NotFound("Activity not found.");

        var fileName = $"{activityId}_{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}{extension}";

        // ── Handle upload ─────────────────────────────────────────────────────
        string? fileUrl = null;
        string? videoLocalPath = null;          // set only for video uploads
        var tags = new List<MediaTag>();
        List<FaceMatchResult> prevalidatedMatches = new(); // image only: faces found before DB write

        if (isImage)
        {
            var path = $"{MediaFolder}/{fileName}";
            await using var uploadStream = file.OpenReadStream();
            await _blobService.UploadFileAsync(fileName, uploadStream, MediaFolder);
            fileUrl = await _blobService.GetPreviewUrlAsync(path);

            // ── Pre-validate faces BEFORE saving to DB ──────────────────────
            // This keeps the DB clean: if no face is found, we delete the S3 file
            // and reject the request without leaving any orphaned MediaAsset row.
            prevalidatedMatches = await _faceRecognitionService.SearchFacesAsync(_blobService.BucketName, path);

            if (prevalidatedMatches.Count == 0)
            {
                _logger.LogWarning("No recognizable face found in uploaded image. Removing S3 object: {Path}", path);
                await _blobService.DeleteByKeyAsync(path);
                throw ErrorHelper.BadRequest(
                    "No recognizable face found in the uploaded image. " +
                    "Please upload a clear photo where a registered student's face is visible.");
            }
        }
        else // isVideo — upload raw to S3; worker submits MediaConvert job
        {
            var rawS3Key = $"{RawFolder}/{fileName}";
            _logger.LogInformation("Uploading raw video to S3: {RawKey}", rawS3Key);
            await using var rawStream = file.OpenReadStream();
            await _blobService.UploadFileAsync(fileName, rawStream, RawFolder);
            videoLocalPath = rawS3Key; // holds raw S3 key (not a local path)
        }

        // ── Save MediaAsset ───────────────────────────────────────────────────
        // For images: only reached if face pre-validation passed above.
        var media = new MediaAsset
        {
            UploaderId = userId,
            ActivityId = activityId,
            FileUrl = fileUrl,   // null for video until transcoding done
            FileType = isImage ? "image" : "video",
            VideoStatus = isVideo ? VideoProcessingStatus.Transcoding : VideoProcessingStatus.None,
            UploadedAt = DateTime.UtcNow
        };

        await _unitOfWork.MediaAssets.AddAsync(media);
        await _unitOfWork.SaveChangesAsync(); // persist before enqueue

        // ── Face Tagging ──────────────────────────────────────────────────────
        if (isImage)
        {
            // Reuse the matches already fetched during pre-validation — no second Rekognition call.
            tags = await SaveFaceTagsAsync(media.Id, prevalidatedMatches);
        }
        else // isVideo — submit MediaConvert job directly
        {
            // Store raw S3 key so the worker can locate the source video.
            media.RawVideoS3Key = videoLocalPath;
            await _unitOfWork.SaveChangesAsync();

            await StartVideoTranscodeAsync(media.Id);
            _logger.LogInformation("Video submitted to MediaConvert. MediaId: {MediaId}, RawKey: {Key}", media.Id, videoLocalPath);
        }

        _logger.LogInformation("UploadMediaAsync completed. MediaId: {MediaId}", media.Id);
        return await MapToDto(media, tags);
    }

    /// <inheritdoc />
    public async Task<List<MediaAssetDto>> GetMediaByActivityAsync(Guid activityId)
    {
        _logger.LogInformation("GetMediaByActivityAsync: ActivityId={ActivityId}", activityId);

        var mediaList = await _unitOfWork.MediaAssets
            .GetAllAsync(m => m.ActivityId == activityId && !m.IsDeleted, m => m.MediaTags);

        // Batch-fetch all referenced students in a single query to avoid N+1.
        var allStudentIds = mediaList
            .SelectMany(m => m.MediaTags)
            .Select(t => t.StudentId)
            .Distinct()
            .ToList();

        var studentMap = allStudentIds.Count > 0
            ? (await _unitOfWork.Users.GetAllAsync(u => allStudentIds.Contains(u.Id)))
              .ToDictionary(u => u.Id)
            : new Dictionary<Guid, User>();

        var result = new List<MediaAssetDto>();

        foreach (var media in mediaList)
        {
            var tagDtos = media.MediaTags.Select(tag =>
            {
                studentMap.TryGetValue(tag.StudentId, out var student);
                return new MediaTagDto
                {
                    StudentId = tag.StudentId,
                    StudentName = student?.FullName,
                    ConfidenceScore = tag.ConfidenceScore,
                    IsVerified = tag.IsVerified
                };
            }).ToList();

            result.Add(new MediaAssetDto
            {
                Id = media.Id,
                UploaderId = media.UploaderId,
                ActivityId = media.ActivityId,
                FileUrl = media.FileUrl,
                FileType = media.FileType,
                FaceSearchJobId = media.FaceSearchJobId,
                VideoStatus = media.VideoStatus,
                UploadedAt = media.UploadedAt,
                Tags = tagDtos
            });
        }

        return result;
    }

    /// <inheritdoc />
    /// Submits a MediaConvert job (non-blocking). After this returns, VideoStatus
    /// is still <see cref="VideoProcessingStatus.Transcoding"/> and
    /// <c>MediaConvertJobId</c> holds the MC job ID.
    public async Task StartVideoTranscodeAsync(Guid mediaId)
    {
        _logger.LogInformation("StartVideoTranscodeAsync: MediaId={MediaId}", mediaId);

        var media = await _unitOfWork.MediaAssets.GetByIdAsync(mediaId);
        if (media == null || media.IsDeleted)
        {
            _logger.LogWarning("StartVideoTranscodeAsync: MediaId={MediaId} not found or deleted.", mediaId);
            return;
        }

        if (media.VideoStatus != VideoProcessingStatus.Transcoding)
        {
            _logger.LogInformation("StartVideoTranscodeAsync: skipping, VideoStatus={Status}", media.VideoStatus);
            return;
        }

        // If a MC job was already submitted (e.g. recovery after crash), skip re-submission.
        if (!string.IsNullOrEmpty(media.MediaConvertJobId))
        {
            _logger.LogInformation(
                "StartVideoTranscodeAsync: MC job already submitted for MediaId={MediaId}. Skipping.",
                mediaId);
            return;
        }

        var rawS3Key = media.RawVideoS3Key;
        if (string.IsNullOrEmpty(rawS3Key))
            throw new InvalidOperationException(
                $"MediaId={mediaId}: expected RawVideoS3Key to be set.");

        try
        {
            // ── Submit MediaConvert job (returns immediately) ─────────────────
            _logger.LogInformation(
                "Submitting MediaConvert job: raw={RawKey} → {Folder}/ for MediaId={MediaId}",
                rawS3Key, MediaFolder, mediaId);

            var mcJobId = await _videoConverterService.SubmitTranscodeJobAsync(
                rawS3Key, $"{MediaFolder}/");

            // ── Persist MC job ID so the worker can poll it ───────────────────
            media.MediaConvertJobId = mcJobId;
            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation(
                "MediaConvert job submitted: {McJobId} for MediaId={MediaId}",
                mcJobId, mediaId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "StartVideoTranscodeAsync failed for MediaId={MediaId}", mediaId);
            media.VideoStatus = VideoProcessingStatus.Failed;
            await _unitOfWork.SaveChangesAsync();
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<bool> TryCompleteTranscodeAsync(Guid mediaId)
    {
        _logger.LogInformation("TryCompleteTranscodeAsync: MediaId={MediaId}", mediaId);

        var media = await _unitOfWork.MediaAssets.GetByIdAsync(mediaId);
        if (media == null || media.IsDeleted)
        {
            _logger.LogWarning("TryCompleteTranscodeAsync: MediaId={MediaId} not found or deleted.", mediaId);
            return false;
        }

        if (media.VideoStatus != VideoProcessingStatus.Transcoding)
        {
            _logger.LogInformation(
                "TryCompleteTranscodeAsync: unexpected VideoStatus={Status} for MediaId={MediaId}",
                media.VideoStatus, mediaId);
            return false;
        }

        var mcJobId = media.MediaConvertJobId;
        if (string.IsNullOrEmpty(mcJobId))
            throw new InvalidOperationException(
                $"MediaId={mediaId}: expected MediaConvertJobId to be set.");

        // ── Poll MediaConvert ─────────────────────────────────────────────────
        var status = await _videoConverterService.GetJobStatusAsync(mcJobId);

        if (status == MediaConvertJobStatus.InProgress)
        {
            _logger.LogInformation(
                "MediaConvert job {McJobId} still in progress for MediaId={MediaId}",
                mcJobId, mediaId);
            return false;
        }

        if (status == MediaConvertJobStatus.Error)
        {
            _logger.LogError(
                "MediaConvert job {McJobId} failed for MediaId={MediaId}",
                mcJobId, mediaId);
            media.VideoStatus = VideoProcessingStatus.Failed;
            await _unitOfWork.SaveChangesAsync();
            throw new InvalidOperationException(
                $"MediaConvert job {mcJobId} failed for MediaId={mediaId}.");
        }

        // ── status == Complete ────────────────────────────────────────────────
        _logger.LogInformation(
            "MediaConvert job {McJobId} completed for MediaId={MediaId}",
            mcJobId, mediaId);

        string outputS3Key;
        string fileUrl;
        string rekJobId;

        try
        {
            outputS3Key = await _videoConverterService.GetOutputS3KeyAsync(mcJobId);
            fileUrl = await _blobService.GetPreviewUrlAsync(outputS3Key);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "TryCompleteTranscodeAsync: failed to resolve output for MediaId={MediaId}", mediaId);
            media.VideoStatus = VideoProcessingStatus.Failed;
            await _unitOfWork.SaveChangesAsync();
            throw;
        }

        // ── Start Rekognition face-search ───────────────────────────────────────────
        try
        {
            _logger.LogInformation(
                "Starting Rekognition video face-search for MediaId={MediaId}, S3Key={Key}",
                mediaId, outputS3Key);
            rekJobId = await _faceRecognitionService.StartVideoFaceSearchAsync(
                _blobService.BucketName, outputS3Key);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "TryCompleteTranscodeAsync: failed to start Rekognition for MediaId={MediaId}", mediaId);
            media.VideoStatus = VideoProcessingStatus.Failed;
            await _unitOfWork.SaveChangesAsync();
            throw;
        }

        // ── Start Rekognition Label Detection (for strengths-based filtering) ──────
        // Fire-and-forget style: failures are non-fatal — the strengths pipeline simply
        // falls back to face-only clipping when LabelJobRef is null or the job fails.
        try
        {
            _logger.LogInformation(
                "Starting Rekognition Label Detection for MediaId={MediaId}, S3Key={Key}",
                mediaId, outputS3Key);
            var labelJobId = await _faceRecognitionService.StartLabelDetectionAsync(
                _blobService.BucketName, outputS3Key);
            media.LabelJobRef = labelJobId;
            _logger.LogInformation(
                "Label Detection job started: {LabelJobId} for MediaId={MediaId}", labelJobId, mediaId);
        }
        catch (Exception ex)
        {
            // Non-fatal: strengths filtering will fall back gracefully.
            _logger.LogWarning(ex,
                "TryCompleteTranscodeAsync: failed to start Label Detection for MediaId={MediaId}. " +
                "Strengths-based filtering will be unavailable for this video.", mediaId);
        }

        // ── Persist state transition ──────────────────────────────────────────────
        media.FileUrl = fileUrl;
        media.FaceSearchJobId = rekJobId;
        media.VideoStatus = VideoProcessingStatus.PendingTagging;
        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation(
            "Rekognition job started: {RekJobId} for MediaId={MediaId}",
            rekJobId, mediaId);

        // ── Delete raw source file from S3 (no longer needed) ──────────────────────
        // The transcoded output is in media/; keeping raw/ wastes storage.
        // Failure to delete is logged but non-fatal.
        try
        {
            var rawS3Key = await _videoConverterService.GetInputS3KeyAsync(mcJobId);
            _logger.LogInformation(
                "Deleting raw S3 file after transcode. Key={RawKey}, MediaId={MediaId}",
                rawS3Key, mediaId);
            await _blobService.DeleteByKeyAsync(rawS3Key);
        }
        catch (Exception cleanupEx)
        {
            _logger.LogWarning(cleanupEx,
                "Failed to delete raw S3 file for MediaId={MediaId}. Manual cleanup may be needed.",
                mediaId);
        }

        return true;
    }

    /// <inheritdoc />
    public async Task<MediaAssetDto> ProcessVideoTagsAsync(Guid mediaId)
    {
        _logger.LogInformation("ProcessVideoTagsAsync: MediaId={MediaId}", mediaId);

        var media = await _unitOfWork.MediaAssets.GetByIdAsync(mediaId, m => m.MediaTags);
        if (media == null || media.IsDeleted)
            throw ErrorHelper.NotFound("Media not found.");

        // Guard: must be in PendingTagging state
        if (media.VideoStatus != VideoProcessingStatus.PendingTagging)
            throw ErrorHelper.BadRequest("Video is not ready for tag processing yet.");

        if (string.IsNullOrEmpty(media.FaceSearchJobId))
            throw ErrorHelper.BadRequest("This media has no pending Rekognition video job.");

        var (success, newTags) = await DoProcessVideoTagsAsync(media);

        if (!success)
            throw ErrorHelper.BadRequest("Video processing is still in progress. Please try again later.");

        return await MapToDto(media, media.MediaTags.Concat(newTags).ToList());
    }

    /// <inheritdoc />
    public async Task<bool> TryProcessVideoTagsAsync(Guid mediaId)
    {
        _logger.LogInformation("TryProcessVideoTagsAsync: MediaId={MediaId}", mediaId);

        var media = await _unitOfWork.MediaAssets.GetByIdAsync(mediaId, m => m.MediaTags);
        if (media == null || media.IsDeleted) return false;
        // Only poll Rekognition once transcoding is done and a real job ID is stored
        if (media.VideoStatus != VideoProcessingStatus.PendingTagging) return false;
        if (string.IsNullOrEmpty(media.FaceSearchJobId)) return false;

        var (success, _) = await DoProcessVideoTagsAsync(media);
        return success;
    }

    /// <inheritdoc />
    public async Task DeleteMediaAsync(Guid mediaId)
    {
        _logger.LogInformation("DeleteMediaAsync: MediaId={MediaId}", mediaId);

        var media = await _unitOfWork.MediaAssets.GetByIdAsync(mediaId);
        if (media == null || media.IsDeleted)
            throw ErrorHelper.NotFound("Media not found.");

        // Delete the file from S3 using the bucket-relative key.
        // FileUrl is a presigned/CDN URL — extract the key before deleting.
        if (!string.IsNullOrWhiteSpace(media.FileUrl))
        {
            try
            {
                var uri = new Uri(media.FileUrl);
                var s3Key = uri.AbsolutePath.TrimStart('/');
                // Strip bucket prefix if present (path-style URL: /{bucket}/{key})
                var bucketPrefix = $"{_blobService.BucketName}/";
                if (s3Key.StartsWith(bucketPrefix, StringComparison.OrdinalIgnoreCase))
                    s3Key = s3Key[bucketPrefix.Length..];

                await _blobService.DeleteByKeyAsync(s3Key);
            }
            catch (UriFormatException)
            {
                // FileUrl is already a raw key (older records)
                await _blobService.DeleteByKeyAsync(media.FileUrl);
            }
        }

        await _unitOfWork.MediaAssets.SoftRemove(media);
        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation("Media deleted: {MediaId}", mediaId);
    }

    /// <inheritdoc />
    public async Task<bool> IsAwaitingTaggingAsync(Guid mediaId)
    {
        var media = await _unitOfWork.MediaAssets.GetByIdAsync(mediaId);
        return media != null
               && !media.IsDeleted
               && media.VideoStatus == VideoProcessingStatus.PendingTagging
               && !string.IsNullOrEmpty(media.FaceSearchJobId);
    }

    /// <inheritdoc />
    public async Task HandleMediaConvertWebhookAsync(string jobId, bool isSuccess)
    {
        var mediaAsset = await _unitOfWork.MediaAssets.FirstOrDefaultAsync(
            m => m.MediaConvertJobId == jobId && !m.IsDeleted);

        if (mediaAsset != null)
        {
            if (isSuccess)
            {
                try
                {
                    await TryCompleteTranscodeAsync(mediaAsset.Id);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "TryCompleteTranscodeAsync failed for MediaId {Id}", mediaAsset.Id);
                }
            }
            else
            {
                mediaAsset.VideoStatus = VideoProcessingStatus.Failed;
                await _unitOfWork.SaveChangesAsync();
            }
        }
    }

    /// <inheritdoc />
    public async Task HandleFaceSearchWebhookAsync(string jobId, bool isSuccess)
    {
        var mediaAsset = await _unitOfWork.MediaAssets.FirstOrDefaultAsync(
            m => m.FaceSearchJobId == jobId && !m.IsDeleted);

        if (mediaAsset != null)
        {
            if (isSuccess)
            {
                try
                {
                    await TryProcessVideoTagsAsync(mediaAsset.Id);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "TryProcessVideoTagsAsync failed for MediaId {Id}", mediaAsset.Id);
                }
            }
            else
            {
                mediaAsset.VideoStatus = VideoProcessingStatus.Failed;
                await _unitOfWork.SaveChangesAsync();
            }
        }
        else
        {
            _logger.LogWarning("HandleFaceSearchWebhookAsync: no MediaAsset found for FaceSearch JobId={JobId}", jobId);
        }
    }

    /// <inheritdoc />
    public async Task HandleLabelDetectionWebhookAsync(string jobId, bool isSuccess)
    {
        if (!isSuccess)
        {
            // Mark the asset so strengths filtering knows the label job failed.
            var labelAsset = await _unitOfWork.MediaAssets.FirstOrDefaultAsync(
                m => m.LabelJobRef == jobId && !m.IsDeleted);

            if (labelAsset != null)
            {
                _logger.LogWarning(
                    "Label Detection job FAILED for MediaId={MediaId}, JobId={JobId}. " +
                    "Clearing LabelJobRef so strengths filter falls back gracefully.",
                    labelAsset.Id, jobId);
                labelAsset.LabelJobRef = null;
                await _unitOfWork.SaveChangesAsync();
            }
            else
            {
                _logger.LogWarning(
                    "Label Detection FAILED for unknown JobId={JobId} (no matching MediaAsset).", jobId);
            }
        }
        else
        {
            _logger.LogInformation(
                "Label Detection job SUCCEEDED for JobId={JobId}. Results available on demand.", jobId);
        }
    }

    // ── Private Helpers ───────────────────────────────────────────────────────

    /// <summary>
    /// Core logic shared by <see cref="ProcessVideoTagsAsync"/> and <see cref="TryProcessVideoTagsAsync"/>.
    /// Returns (true, newTags) when the Rekognition job succeeded and tags were saved,
    /// or (false, empty) when the job is still IN_PROGRESS.
    /// Throws on FAILED status.
    /// </summary>
    private async Task<(bool Done, List<MediaTag> NewTags)> DoProcessVideoTagsAsync(MediaAsset media)
    {
        var result = await _faceRecognitionService.GetVideoFaceSearchResultsAsync(media.FaceSearchJobId!);

        if (result == null)
            return (false, new List<MediaTag>()); // still IN_PROGRESS

        if (result.JobStatus == "FAILED")
        {
            _logger.LogWarning("Rekognition job FAILED for MediaId: {MediaId}, JobId: {JobId}",
                media.Id, media.FaceSearchJobId);
            media.VideoStatus = VideoProcessingStatus.Failed;
            await _unitOfWork.SaveChangesAsync();
            throw ErrorHelper.Internal("Video face recognition job failed.");
        }

        var newTags = new List<MediaTag>();

        // Capture per-student appearance timelines NOW, while the Rekognition job results
        // are still available (Rekognition retains video job results for only 7 days).
        // Persisting them lets the personal-video pipeline build clips indefinitely without
        // re-querying Rekognition. A null result means the job is not SUCCEEDED — unexpected
        // here since result.JobStatus was already checked, so we just skip persistence.
        Dictionary<Guid, VideoFaceTimelineResult>? timelines = null;
        try
        {
            timelines = await _faceRecognitionService.GetAllFaceTimelinesAsync(media.FaceSearchJobId!);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "DoProcessVideoTagsAsync: failed to capture face timelines for MediaId={MediaId}. " +
                "Personal video generation will fall back to the safe legacy policy.", media.Id);
        }

        // Batch-load all matched students in one query to avoid N+1 DB round-trips.
        var matchedUserIds = result.Matches
            .Select(m => m.UserId)
            .Distinct()
            .ToList();
        var studentMap = matchedUserIds.Count > 0
            ? (await _unitOfWork.Users.GetAllAsync(u => matchedUserIds.Contains(u.Id)))
              .ToDictionary(u => u.Id)
            : new Dictionary<Guid, User>();

        foreach (var match in result.Matches)
        {
            // Skip duplicates already in DB
            if (media.MediaTags.Any(t => t.StudentId == match.UserId))
                continue;

            if (!studentMap.TryGetValue(match.UserId, out var student))
            {
                _logger.LogWarning("Skipping face match for non-existent UserId: {UserId} (FaceId: {FaceId})",
                    match.UserId, match.FaceId);
                continue;
            }

            var tag = new MediaTag
            {
                MediaId = media.Id,
                StudentId = match.UserId,
                ConfidenceScore = (decimal)match.Confidence,
                IsVerified = true
            };
            await _unitOfWork.MediaTags.AddAsync(tag);
            newTags.Add(tag);
        }

        // Persist captured timelines onto every tag (new + existing) for this media.
        if (timelines != null)
        {
            foreach (var tag in media.MediaTags.Concat(newTags))
            {
                if (timelines.TryGetValue(tag.StudentId, out var timeline))
                {
                    tag.FaceSegmentsJson = JsonSerializer.Serialize(timeline.Segments);
                    tag.HasOtherFaces = timeline.HasOtherFaces;
                }
            }
        }

        media.VideoStatus = VideoProcessingStatus.TaggingComplete;
        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation("DoProcessVideoTagsAsync completed. {Count} new tag(s) for MediaId: {MediaId}",
            newTags.Count, media.Id);
        return (true, newTags);
    }

    /// <summary>
    /// Persists <see cref="MediaTag"/> rows from already-fetched Rekognition matches.
    /// Called for images after face pre-validation in <see cref="UploadMediaAsync"/>.
    /// </summary>
    private async Task<List<MediaTag>> SaveFaceTagsAsync(Guid mediaId, List<FaceMatchResult> matches)
    {
        var tags = new List<MediaTag>();

        // Batch-load all matched students in one query to avoid N+1 DB round-trips.
        var matchedUserIds = matches.Select(m => m.UserId).Distinct().ToList();
        var studentMap = matchedUserIds.Count > 0
            ? (await _unitOfWork.Users.GetAllAsync(u => matchedUserIds.Contains(u.Id)))
              .ToDictionary(u => u.Id)
            : new Dictionary<Guid, User>();

        foreach (var match in matches)
        {
            if (!studentMap.TryGetValue(match.UserId, out var student))
            {
                _logger.LogWarning("Skipping face match for non-existent UserId: {UserId} (FaceId: {FaceId})",
                    match.UserId, match.FaceId);
                continue;
            }

            var tag = new MediaTag
            {
                MediaId = mediaId,
                StudentId = match.UserId,
                ConfidenceScore = (decimal)match.Confidence,
                IsVerified = true
            };
            await _unitOfWork.MediaTags.AddAsync(tag);
            tags.Add(tag);
        }

        _logger.LogInformation("Image face-tagged: {Count} match(es) for Media: {MediaId}", tags.Count, mediaId);
        await _unitOfWork.SaveChangesAsync();
        return tags;
    }

    private async Task<MediaAssetDto> MapToDto(MediaAsset media, ICollection<MediaTag>? tags = null)
    {
        var tagDtos = new List<MediaTagDto>();
        if (tags != null && tags.Count > 0)
        {
            // Batch-fetch all students in one query to avoid N+1.
            var studentIds = tags.Select(t => t.StudentId).Distinct().ToList();
            var studentMap = (await _unitOfWork.Users.GetAllAsync(u => studentIds.Contains(u.Id)))
                .ToDictionary(u => u.Id);

            foreach (var t in tags)
            {
                studentMap.TryGetValue(t.StudentId, out var student);
                tagDtos.Add(new MediaTagDto
                {
                    StudentId = t.StudentId,
                    StudentName = student?.FullName,
                    ConfidenceScore = t.ConfidenceScore,
                    IsVerified = t.IsVerified
                });
            }
        }

        return new MediaAssetDto
        {
            Id = media.Id,
            UploaderId = media.UploaderId,
            ActivityId = media.ActivityId,
            FileUrl = media.FileUrl,
            FileType = media.FileType,
            FaceSearchJobId = media.FaceSearchJobId,
            VideoStatus = media.VideoStatus,
            UploadedAt = media.UploadedAt,
            Tags = tagDtos
        };
    }
}
