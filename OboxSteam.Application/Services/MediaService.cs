using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using OboxSteam.Application.DTOs.MediaDTO;
using OboxSteam.Application.Interfaces;
using OboxSteam.Application.Utils;
using OboxSteam.Domain.Entities;
using OboxSteam.Domain.Interfaces;

namespace OboxSteam.Application.Services;

public class MediaService : IMediaService
{
    private static readonly HashSet<string> ImageExtensions = new(StringComparer.OrdinalIgnoreCase)
        { ".jpg", ".jpeg", ".png" };

    private static readonly HashSet<string> VideoExtensions = new(StringComparer.OrdinalIgnoreCase)
        { ".mp4", ".mov" };

    private const long MaxImageSize = 10 * 1024 * 1024;  // 10 MB
    private const long MaxVideoSize = 50 * 1024 * 1024;  // 50 MB
    private const string MediaFolder = "media";
    private const string S3Bucket = "oboxsteam-bucket";

    private readonly IClaimsService _claimsService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IBlobService _blobService;
    private readonly IFaceRecognitionService _faceRecognitionService;
    private readonly ILogger<MediaService> _logger;

    public MediaService(
        IClaimsService claimsService,
        IUnitOfWork unitOfWork,
        IBlobService blobService,
        IFaceRecognitionService faceRecognitionService,
        ILogger<MediaService> logger)
    {
        _claimsService = claimsService;
        _unitOfWork = unitOfWork;
        _blobService = blobService;
        _faceRecognitionService = faceRecognitionService;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<MediaAssetDto> UploadMediaAsync(IFormFile file, Guid activityId)
    {
        var userId = _claimsService.GetCurrentUserId;
        _logger.LogInformation("UploadMediaAsync started by UserId: {UserId} for Activity: {ActivityId}", userId, activityId);

        // ── Validate ────────────────────────────────────
        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        var isImage = ImageExtensions.Contains(extension);
        var isVideo = VideoExtensions.Contains(extension);

        if (!isImage && !isVideo)
            throw ErrorHelper.BadRequest("Only image (.jpg, .jpeg, .png) and video (.mp4, .mov) files are allowed.");

        if (isImage && file.Length > MaxImageSize)
            throw ErrorHelper.BadRequest("Image file size must not exceed 10 MB.");

        if (isVideo && file.Length > MaxVideoSize)
            throw ErrorHelper.BadRequest("Video file size must not exceed 50 MB.");

        // Verify activity exists
        var activity = await _unitOfWork.Activities.GetByIdAsync(activityId);
        if (activity == null || activity.IsDeleted)
            throw ErrorHelper.NotFound("Activity not found.");

        // ── Upload to S3 first ──────────────────────────
        // Upload phải diễn ra trước face search vì:
        // 1. Tránh lỗi stream bị consumed khi đọc 2 lần
        // 2. SearchFacesAsync dùng S3Object thay vì Bytes, tránh giới hạn 5MB của Rekognition
        var fileName = $"{activityId}_{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}{extension}";
        var s3Key = $"{MediaFolder}/{fileName}";

        await using var uploadStream = file.OpenReadStream();
        await _blobService.UploadFileAsync(fileName, uploadStream, MediaFolder);

        var fileUrl = await _blobService.GetPreviewUrlAsync(s3Key);

        // ── Save MediaAsset ─────────────────────────────
        var media = new MediaAsset
        {
            UploaderId = userId,
            ActivityId = activityId,
            FileUrl = fileUrl,
            FileType = isImage ? "image" : "video",
            UploadedAt = DateTime.UtcNow
        };

        await _unitOfWork.MediaAssets.AddAsync(media);

        // ── Face Tagging ────────────────────────────────
        var tags = new List<MediaTag>();

        if (isImage)
        {
            // Truyền s3Key thay vì stream — Rekognition đọc trực tiếp từ S3
            tags = await TagImageFacesAsync(media.Id, S3Bucket, s3Key);
        }
        else if (isVideo)
        {
            var jobId = await _faceRecognitionService.StartVideoFaceSearchAsync(S3Bucket, s3Key);
            media.RekognitionJobId = jobId;
            _logger.LogInformation("Video face search job started: {JobId} for Media: {MediaId}", jobId, media.Id);
        }

        await _unitOfWork.SaveChangesAsync();

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
                    StudentId = tag.StudentId,
                    StudentName = student?.FullName,
                    ConfidenceScore = tag.ConfidenceScore,
                    IsVerified = tag.IsVerified
                });
            }

            result.Add(new MediaAssetDto
            {
                Id = media.Id,
                UploaderId = media.UploaderId,
                ActivityId = media.ActivityId,
                FileUrl = media.FileUrl,
                FileType = media.FileType,
                RekognitionJobId = media.RekognitionJobId,
                UploadedAt = media.UploadedAt,
                Tags = tagDtos
            });
        }

        return result;
    }

    /// <inheritdoc />
    public async Task<MediaAssetDto> ProcessVideoTagsAsync(Guid mediaId)
    {
        _logger.LogInformation("ProcessVideoTagsAsync: MediaId={MediaId}", mediaId);

        var media = await _unitOfWork.MediaAssets.GetByIdAsync(mediaId, m => m.MediaTags);
        if (media == null || media.IsDeleted)
            throw ErrorHelper.NotFound("Media not found.");

        if (string.IsNullOrEmpty(media.RekognitionJobId))
            throw ErrorHelper.BadRequest("This media has no pending Rekognition video job.");

        var result = await _faceRecognitionService.GetVideoFaceSearchResultsAsync(media.RekognitionJobId);

        if (result == null)
            throw ErrorHelper.BadRequest("Video processing is still in progress. Please try again later.");

        if (result.JobStatus == "FAILED")
            throw ErrorHelper.Internal("Video face recognition job failed.");

        var tags = new List<MediaTag>();
        foreach (var match in result.Matches)
        {
            if (media.MediaTags.Any(t => t.StudentId == match.UserId))
                continue;

            var student = await _unitOfWork.Users.GetByIdAsync(match.UserId);
            if (student == null)
            {
                _logger.LogWarning("Skipping face match for non-existent UserId: {UserId} (FaceId: {FaceId})", match.UserId, match.FaceId);
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

        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation("ProcessVideoTagsAsync completed. {Count} new tag(s) created.", tags.Count);
        return await MapToDto(media, media.MediaTags.Concat(tags).ToList());
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

    // ── Private Helpers ─────────────────────────────

    private async Task<List<MediaTag>> TagImageFacesAsync(Guid mediaId, string s3Bucket, string s3Key)
    {
        var tags = new List<MediaTag>();

        // Dùng S3Object thay vì Stream — tránh lỗi 5MB limit và stream consumed
        var matches = await _faceRecognitionService.SearchFacesAsync(s3Bucket, s3Key);

        foreach (var match in matches)
        {
            var student = await _unitOfWork.Users.GetByIdAsync(match.UserId);
            if (student == null)
            {
                _logger.LogWarning("Skipping face match for non-existent UserId: {UserId} (FaceId: {FaceId})", match.UserId, match.FaceId);
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
            RekognitionJobId = media.RekognitionJobId,
            UploadedAt = media.UploadedAt,
            Tags = tagDtos
        };
    }
}