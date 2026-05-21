using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using OboxSteam.Application.Commons;
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
    private const string S3Bucket = "oboxsteam-bucket";

    // Prefixes stored temporarily in VideoJobRef to distinguish pipeline stages:
    // "raw:{s3key}"  → raw video uploaded to S3, MediaConvert job not yet submitted
    // "mc:{jobId}"   → MediaConvert job submitted, waiting for completion
    // (plain)        → real Rekognition job ID (VideoStatus = PendingTagging)
    private const string RawPrefix = "raw:";
    private const string McPrefix = "mc:";

    private readonly IClaimsService _claimsService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IBlobService _blobService;
    private readonly IFaceRecognitionService _faceRecognitionService;
    private readonly ILogger<MediaService> _logger;
    private readonly VideoProcessingChannel _videoChannel;
    private readonly IVideoConverterService _videoConverterService;

    public MediaService(
        IClaimsService claimsService,
        IUnitOfWork unitOfWork,
        IBlobService blobService,
        IFaceRecognitionService faceRecognitionService,
        ILogger<MediaService> logger,
        VideoProcessingChannel videoChannel,
        IVideoConverterService videoConverterService)
    {
        _claimsService = claimsService;
        _unitOfWork = unitOfWork;
        _blobService = blobService;
        _faceRecognitionService = faceRecognitionService;
        _logger = logger;
        _videoChannel = videoChannel;
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

            // ── Pre-validate faces BEFORE saving to DB ────────────────────────
            // This keeps the DB clean: if no face is found, we delete the S3 file
            // and reject the request without leaving any orphaned MediaAsset row.
            try
            {
                prevalidatedMatches = await _faceRecognitionService.SearchFacesAsync(S3Bucket, path);
            }
            catch
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
            videoLocalPath = rawS3Key; // repurposed: holds raw S3 key (not a local path)
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
        else // isVideo — enqueue for background processing
        {
            // Store raw S3 key (prefixed) so the worker can locate the source video.
            media.VideoJobRef = $"{RawPrefix}{videoLocalPath}";
            await _unitOfWork.SaveChangesAsync();

            await _videoChannel.Writer.WriteAsync(media.Id);
            _logger.LogInformation("Video enqueued for MediaConvert. MediaId: {MediaId}, RawKey: {Key}", media.Id, videoLocalPath);
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
                VideoJobRef = media.VideoJobRef,
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
    /// <c>VideoJobRef</c> holds the MC job ID prefixed with "mc:".
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

        var stored = media.VideoJobRef
            ?? throw new InvalidOperationException(
                $"MediaId={mediaId} has no value stored in VideoJobRef.");

        // If a MC job was already submitted (e.g. recovery after crash), skip re-submission.
        if (stored.StartsWith(McPrefix, StringComparison.Ordinal))
        {
            _logger.LogInformation(
                "StartVideoTranscodeAsync: MC job already submitted for MediaId={MediaId}. Skipping.",
                mediaId);
            return;
        }

        if (!stored.StartsWith(RawPrefix, StringComparison.Ordinal))
            throw new InvalidOperationException(
                $"MediaId={mediaId}: unexpected value in VideoJobRef: '{stored}'.");

        var rawS3Key = stored[RawPrefix.Length..]; // strip "raw:" prefix

        try
        {
            // ── Submit MediaConvert job (returns immediately) ─────────────────
            _logger.LogInformation(
                "Submitting MediaConvert job: raw={RawKey} → {Folder}/ for MediaId={MediaId}",
                rawS3Key, MediaFolder, mediaId);

            var mcJobId = await _videoConverterService.SubmitTranscodeJobAsync(
                rawS3Key, $"{MediaFolder}/");

            // ── Persist MC job ID so the worker can poll it ───────────────────
            media.VideoJobRef = $"{McPrefix}{mcJobId}";
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

        var stored = media.VideoJobRef ?? string.Empty;
        if (!stored.StartsWith(McPrefix, StringComparison.Ordinal))
            throw new InvalidOperationException(
                $"MediaId={mediaId}: expected 'mc:' prefix in VideoJobRef, got: '{stored}'.");

        var mcJobId = stored[McPrefix.Length..]; // strip "mc:"

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

        try
        {
            var outputS3Key = await ResolveMediaConvertOutputKeyAsync(mcJobId);

            // ── Build public URL ──────────────────────────────────────────────
            var fileUrl = await _blobService.GetPreviewUrlAsync(outputS3Key);

            // ── Start Rekognition face-search ─────────────────────────────────
            _logger.LogInformation(
                "Starting Rekognition video face-search for MediaId={MediaId}, S3Key={Key}",
                mediaId, outputS3Key);
            var rekJobId = await _faceRecognitionService.StartVideoFaceSearchAsync(
                S3Bucket, outputS3Key);

            // ── Persist state transition ──────────────────────────────────────
            media.FileUrl = fileUrl;
            media.VideoJobRef = rekJobId;   // now holds the real Rekognition job ID
            media.VideoStatus = VideoProcessingStatus.PendingTagging;
            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation(
                "Rekognition job started: {RekJobId} for MediaId={MediaId}",
                rekJobId, mediaId);

            // ── Delete raw source file from S3 (no longer needed) ─────────────
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
        catch (Exception ex)
        {
            _logger.LogError(ex, "TryCompleteTranscodeAsync post-processing failed for MediaId={MediaId}", mediaId);
            media.VideoStatus = VideoProcessingStatus.Failed;
            await _unitOfWork.SaveChangesAsync();
            throw ErrorHelper.Internal("Video processing failed after transcoding. Please try again later.");
        }
    }

    /// <inheritdoc />
    public async Task<MediaAssetDto> ProcessVideoTagsAsync(Guid mediaId)
    {
        _logger.LogInformation("ProcessVideoTagsAsync: MediaId={MediaId}", mediaId);

        var media = await _unitOfWork.MediaAssets.GetByIdAsync(mediaId, m => m.MediaTags);
        if (media == null || media.IsDeleted)
            throw ErrorHelper.NotFound("Media not found.");

        // Guard: must be in PendingTagging state — not Transcoding (where VideoJobRef is a /tmp path)
        if (media.VideoStatus != VideoProcessingStatus.PendingTagging)
            throw ErrorHelper.BadRequest("Video is not ready for tag processing yet.");

        if (string.IsNullOrEmpty(media.VideoJobRef))
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
        if (string.IsNullOrEmpty(media.VideoJobRef)) return false;

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

        if (!string.IsNullOrWhiteSpace(media.FileUrl))
        {
            await _blobService.DeleteFileAsync(media.FileUrl);
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
               && !string.IsNullOrEmpty(media.VideoJobRef);
    }

    // ── Private Helpers ───────────────────────────────────────────────────────

    /// <summary>
    /// Queries the MediaConvert job to find the actual S3 key of the transcoded output file.
    /// MediaConvert outputs to the destination prefix; the file name is derived from the
    /// input file base name (without extension) + ".mp4".
    /// </summary>
    private async Task<string> ResolveMediaConvertOutputKeyAsync(string mcJobId)
    {
        return await _videoConverterService.GetOutputS3KeyAsync(mcJobId);
    }

    /// <summary>
    /// Core logic shared by <see cref="ProcessVideoTagsAsync"/> and <see cref="TryProcessVideoTagsAsync"/>.
    /// Returns (true, newTags) when the Rekognition job succeeded and tags were saved,
    /// or (false, empty) when the job is still IN_PROGRESS.
    /// Throws on FAILED status.
    /// </summary>
    private async Task<(bool Done, List<MediaTag> NewTags)> DoProcessVideoTagsAsync(MediaAsset media)
    {
        var result = await _faceRecognitionService.GetVideoFaceSearchResultsAsync(media.VideoJobRef!);

        if (result == null)
            return (false, new List<MediaTag>()); // still IN_PROGRESS

        if (result.JobStatus == "FAILED")
        {
            _logger.LogWarning("Rekognition job FAILED for MediaId: {MediaId}, JobId: {JobId}",
                media.Id, media.VideoJobRef);
            media.VideoStatus = VideoProcessingStatus.Failed;
            await _unitOfWork.SaveChangesAsync();
            throw ErrorHelper.Internal("Video face recognition job failed.");
        }

        var newTags = new List<MediaTag>();
        foreach (var match in result.Matches)
        {
            // Skip duplicates already in DB
            if (media.MediaTags.Any(t => t.StudentId == match.UserId))
                continue;

            var student = await _unitOfWork.Users.GetByIdAsync(match.UserId);
            if (student == null)
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

        foreach (var match in matches)
        {
            var student = await _unitOfWork.Users.GetByIdAsync(match.UserId);
            if (student == null)
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

        // Hide the internal /tmp path that's temporarily stored in VideoJobRef during transcoding.
        // Clients should only see a real Rekognition job ID (once transcoding completes).
        var VideoJobRef = media.VideoStatus == VideoProcessingStatus.Transcoding
            ? null
            : media.VideoJobRef;

        return new MediaAssetDto
        {
            Id = media.Id,
            UploaderId = media.UploaderId,
            ActivityId = media.ActivityId,
            FileUrl = media.FileUrl,
            FileType = media.FileType,
            VideoJobRef = VideoJobRef,
            VideoStatus = media.VideoStatus,
            UploadedAt = media.UploadedAt,
            Tags = tagDtos
        };
    }
}
