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
    private const string RawFolder   = "raw";
    private const string S3Bucket    = "oboxsteam-bucket";

    // Prefixes stored temporarily in RekognitionJobId to distinguish pipeline stages:
    // "raw:{s3key}"  → raw video uploaded to S3, MediaConvert job not yet submitted
    // "mc:{jobId}"   → MediaConvert job submitted, waiting for completion
    // (plain)        → real Rekognition job ID (VideoStatus = PendingTagging)
    private const string RawPrefix = "raw:";
    private const string McPrefix  = "mc:";

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
        string? fileUrl       = null;
        string? videoLocalPath = null;          // set only for video uploads
        var tags = new List<MediaTag>();

        if (isImage)
        {
            var path = $"{MediaFolder}/{fileName}";
            await using var uploadStream = file.OpenReadStream();
            await _blobService.UploadFileAsync(fileName, uploadStream, MediaFolder);
            fileUrl = await _blobService.GetPreviewUrlAsync(path);
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
        var media = new MediaAsset
        {
            UploaderId  = userId,
            ActivityId  = activityId,
            FileUrl     = fileUrl,   // null for video until transcoding done
            FileType    = isImage ? "image" : "video",
            VideoStatus = isVideo ? VideoProcessingStatus.Transcoding : VideoProcessingStatus.None,
            UploadedAt  = DateTime.UtcNow
        };

        await _unitOfWork.MediaAssets.AddAsync(media);
        await _unitOfWork.SaveChangesAsync(); // persist before enqueue

        // ── Face Tagging ──────────────────────────────────────────────────────
        if (isImage)
        {
            var path = $"{MediaFolder}/{fileName}";
            tags = await TagImageFacesAsync(media.Id, S3Bucket, path);
        }
        else // isVideo — enqueue for background processing
        {
            // Store raw S3 key (prefixed) so the worker can locate the source video.
            media.RekognitionJobId = $"{RawPrefix}{videoLocalPath}";
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

        var result = new List<MediaAssetDto>();

        foreach (var media in mediaList)
        {
            var tagDtos = new List<MediaTagDto>();
            foreach (var tag in media.MediaTags)
            {
                var student = await _unitOfWork.Users.GetByIdAsync(tag.StudentId);
                tagDtos.Add(new MediaTagDto
                {
                    StudentId       = tag.StudentId,
                    StudentName     = student?.FullName,
                    ConfidenceScore = tag.ConfidenceScore,
                    IsVerified      = tag.IsVerified
                });
            }

            result.Add(new MediaAssetDto
            {
                Id               = media.Id,
                UploaderId       = media.UploaderId,
                ActivityId       = media.ActivityId,
                FileUrl          = media.FileUrl,
                FileType         = media.FileType,
                RekognitionJobId = media.RekognitionJobId,
                VideoStatus      = media.VideoStatus,
                UploadedAt       = media.UploadedAt,
                Tags             = tagDtos
            });
        }

        return result;
    }

    /// <inheritdoc />
    /// Submits a MediaConvert job (non-blocking). After this returns, VideoStatus
    /// is still <see cref="VideoProcessingStatus.Transcoding"/> and
    /// <c>RekognitionJobId</c> holds the MC job ID prefixed with "mc:".
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

        var stored = media.RekognitionJobId
            ?? throw new InvalidOperationException(
                $"MediaId={mediaId} has no value stored in RekognitionJobId.");

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
                $"MediaId={mediaId}: unexpected value in RekognitionJobId: '{stored}'.");

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
            media.RekognitionJobId = $"{McPrefix}{mcJobId}";
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

        var stored = media.RekognitionJobId ?? string.Empty;
        if (!stored.StartsWith(McPrefix, StringComparison.Ordinal))
            throw new InvalidOperationException(
                $"MediaId={mediaId}: expected 'mc:' prefix in RekognitionJobId, got: '{stored}'.");

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
            // Derive the output S3 key: media/{baseName}.mp4
            // MediaConvert preserves the input base name, changes extension to .mp4
            // Raw key format: "raw/{activityId}_{timestamp}.{originalExt}"
            // We need to get the raw S3 key from DB history — but we already replaced it with mc:.
            // Instead, compute from the fileName stored in the job or derive from media record.
            // Strategy: find the output file by listing S3 or use a deterministic key.
            // Since UploadMediaAsync uses fileName = "{activityId}_{timestamp}{ext}",
            // the output key = "media/{activityId}_{timestamp}.mp4".
            // We can't recover the original raw key here. Instead, list objects or
            // use the MediaConvert job output path which we set to "media/".
            // Simplest: query the MC job for its output file list.
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
            media.FileUrl          = fileUrl;
            media.RekognitionJobId = rekJobId;   // now holds the real Rekognition job ID
            media.VideoStatus      = VideoProcessingStatus.PendingTagging;
            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation(
                "Rekognition job started: {RekJobId} for MediaId={MediaId}",
                rekJobId, mediaId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "TryCompleteTranscodeAsync post-processing failed for MediaId={MediaId}", mediaId);
            media.VideoStatus = VideoProcessingStatus.Failed;
            await _unitOfWork.SaveChangesAsync();
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<MediaAssetDto> ProcessVideoTagsAsync(Guid mediaId)
    {
        _logger.LogInformation("ProcessVideoTagsAsync: MediaId={MediaId}", mediaId);

        var media = await _unitOfWork.MediaAssets.GetByIdAsync(mediaId, m => m.MediaTags);
        if (media == null || media.IsDeleted)
            throw ErrorHelper.NotFound("Media not found.");

        // Guard: must be in PendingTagging state — not Transcoding (where RekognitionJobId is a /tmp path)
        if (media.VideoStatus != VideoProcessingStatus.PendingTagging)
            throw ErrorHelper.BadRequest("Video is not ready for tag processing yet.");

        if (string.IsNullOrEmpty(media.RekognitionJobId))
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
        if (string.IsNullOrEmpty(media.RekognitionJobId)) return false;

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

    // ── Private Helpers ───────────────────────────────────────────────────────

    /// <summary>
    /// Queries the MediaConvert job to find the actual S3 key of the transcoded output file.
    /// MediaConvert outputs to the destination prefix; the file name is derived from the
    /// input file base name (without extension) + ".mp4".
    /// </summary>
    private async Task<string> ResolveMediaConvertOutputKeyAsync(string mcJobId)
    {
        // The VideoConverterService sets output destination to "s3://bucket/media/".
        // MediaConvert names the file: {inputBaseName}{nameModifier}.mp4
        // Since NameModifier is "", output = {inputBaseName}.mp4
        // We retrieve this from the job's output file metadata.
        // As a fallback we re-query the job via IVideoConverterService — but here
        // we resolve it directly from the job details stored in the MC response.
        // Because we don't have direct access to the MC client here (it's in Infrastructure),
        // we use the deterministic naming convention MediaConvert follows.
        //
        // Note: if the input was "raw/abc_123.mov", MediaConvert outputs "media/abc_123.mp4".
        // We cannot reconstruct the input base name here unless we stored it.
        // Solution: the worker passes mcJobId to IVideoConverterService.GetOutputKeyAsync.
        // For now, we delegate to the infrastructure service.
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
        var result = await _faceRecognitionService.GetVideoFaceSearchResultsAsync(media.RekognitionJobId!);

        if (result == null)
            return (false, new List<MediaTag>()); // still IN_PROGRESS

        if (result.JobStatus == "FAILED")
        {
            _logger.LogWarning("Rekognition job FAILED for MediaId: {MediaId}, JobId: {JobId}",
                media.Id, media.RekognitionJobId);
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
                MediaId         = media.Id,
                StudentId       = match.UserId,
                ConfidenceScore = (decimal)match.Confidence,
                IsVerified      = true
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

    private async Task<List<MediaTag>> TagImageFacesAsync(Guid mediaId, string s3Bucket, string s3Key)
    {
        var tags = new List<MediaTag>();

        var matches = await _faceRecognitionService.SearchFacesAsync(s3Bucket, s3Key);

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
                MediaId         = mediaId,
                StudentId       = match.UserId,
                ConfidenceScore = (decimal)match.Confidence,
                IsVerified      = true
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
        if (tags != null)
        {
            foreach (var t in tags)
            {
                var student = await _unitOfWork.Users.GetByIdAsync(t.StudentId);
                tagDtos.Add(new MediaTagDto
                {
                    StudentId       = t.StudentId,
                    StudentName     = student?.FullName,
                    ConfidenceScore = t.ConfidenceScore,
                    IsVerified      = t.IsVerified
                });
            }
        }

        // Hide the internal /tmp path that's temporarily stored in RekognitionJobId during transcoding.
        // Clients should only see a real Rekognition job ID (once transcoding completes).
        var rekognitionJobId = media.VideoStatus == VideoProcessingStatus.Transcoding
            ? null
            : media.RekognitionJobId;

        return new MediaAssetDto
        {
            Id               = media.Id,
            UploaderId       = media.UploaderId,
            ActivityId       = media.ActivityId,
            FileUrl          = media.FileUrl,
            FileType         = media.FileType,
            RekognitionJobId = rekognitionJobId,
            VideoStatus      = media.VideoStatus,
            UploadedAt       = media.UploadedAt,
            Tags             = tagDtos
        };
    }
}