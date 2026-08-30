using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using OboxSteam.Application.DTOs.MediaDTO;
using OboxSteam.Application.Interfaces;
using OboxSteam.Application.Utils;
using OboxSteam.Application.Validation;
using OboxSteam.Domain.Entities;
using OboxSteam.Domain.Enums;
using OboxSteam.Domain.Interfaces;

namespace OboxSteam.Application.Services;

/// <summary>
/// Offline mentor evidence: image-only uploads without face recognition,
/// stored under <c>session-evidence/{classSessionId}/</c>.
/// </summary>
public sealed class SessionEvidenceService : ISessionEvidenceService
{
    private static readonly HashSet<string> ImageExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png",
    };

    private const long MaxImageSize = 10L * 1024 * 1024;
    private const string EvidenceFolderPrefix = "session-evidence";

    private readonly IUnitOfWork _unitOfWork;
    private readonly IClaimsService _claimsService;
    private readonly IBlobService _blobService;
    private readonly ILogger<SessionEvidenceService> _logger;

    public SessionEvidenceService(
        IUnitOfWork unitOfWork,
        IClaimsService claimsService,
        IBlobService blobService,
        ILogger<SessionEvidenceService> logger)
    {
        _unitOfWork = unitOfWork;
        _claimsService = claimsService;
        _blobService = blobService;
        _logger = logger;
    }

    public async Task<MediaAssetDto> UploadEvidenceAsync(Guid classSessionId, IFormFile file)
    {
        if (file == null || file.Length == 0)
            throw ErrorHelper.BadRequest("Evidence image file is required.");

        var classSession = await _unitOfWork.ClassSessions.GetByIdAsync(classSessionId);
        ClassSessionValidator.ValidateClassSessionExists(classSession, classSessionId);

        await SessionAttendanceValidator.EnsureCanUpdateSessionAttendanceAsync(
            _unitOfWork,
            _claimsService,
            classSession!);

        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!ImageExtensions.Contains(extension))
            throw ErrorHelper.BadRequest("Only image (.jpg, .jpeg, .png) files are allowed for session evidence.");

        if (file.Length > MaxImageSize)
            throw ErrorHelper.BadRequest("Evidence image size must not exceed 10 MB.");

        var userId = _claimsService.GetCurrentUserId;
        var folder = $"{EvidenceFolderPrefix}/{classSessionId:D}";
        var fileName = $"{classSessionId:N}_{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}{extension}";
        var path = $"{folder}/{fileName}";

        await using var stream = file.OpenReadStream();
        await _blobService.UploadFileAsync(fileName, stream, folder);
        var fileUrl = await _blobService.GetPreviewUrlAsync(path);

        var media = new MediaAsset
        {
            UploaderId = userId,
            ClassId = classSession!.ClassId,
            ClassSessionId = classSessionId,
            FileUrl = fileUrl,
            FileType = "image",
            VideoStatus = VideoProcessingStatus.None,
            UploadedAt = DateTime.UtcNow,
        };

        await _unitOfWork.MediaAssets.AddAsync(media);
        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation(
            "Session evidence uploaded. MediaId={MediaId}, ClassSessionId={ClassSessionId}",
            media.Id,
            classSessionId);

        return MapToDto(media);
    }

    public async Task<IReadOnlyList<MediaAssetDto>> ListEvidenceAsync(Guid classSessionId)
    {
        var classSession = await _unitOfWork.ClassSessions.GetByIdAsync(classSessionId);
        ClassSessionValidator.ValidateClassSessionExists(classSession, classSessionId);

        await SessionAttendanceValidator.EnsureCanViewSessionRosterAsync(
            _unitOfWork,
            _claimsService,
            classSession!);

        var mediaList = await _unitOfWork.MediaAssets.GetAllAsync(
            m => m.ClassSessionId == classSessionId
                 && !m.IsDeleted
                 && m.FileType == "image");

        return mediaList
            .OrderByDescending(m => m.UploadedAt ?? m.CreatedAt)
            .Select(MapToDto)
            .ToList();
    }

    public async Task DeleteEvidenceAsync(Guid classSessionId, Guid mediaId)
    {
        var classSession = await _unitOfWork.ClassSessions.GetByIdAsync(classSessionId);
        ClassSessionValidator.ValidateClassSessionExists(classSession, classSessionId);

        await SessionAttendanceValidator.EnsureCanUpdateSessionAttendanceAsync(
            _unitOfWork,
            _claimsService,
            classSession!);

        var media = await _unitOfWork.MediaAssets.GetByIdAsync(mediaId);
        if (media == null || media.IsDeleted)
            throw ErrorHelper.NotFound("Evidence media not found.");

        if (media.ClassSessionId != classSessionId)
            throw ErrorHelper.BadRequest("Media does not belong to this class session.");

        if (!string.IsNullOrEmpty(media.FileUrl))
            await _blobService.DeleteFileAsync(media.FileUrl);

        await _unitOfWork.MediaAssets.SoftRemove(media);
        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation(
            "Session evidence deleted. MediaId={MediaId}, ClassSessionId={ClassSessionId}",
            mediaId,
            classSessionId);
    }

    /// <summary>True when the session has at least one non-deleted image evidence asset.</summary>
    public static bool HasSessionImageEvidence(IEnumerable<MediaAsset> mediaAssets, Guid classSessionId)
    {
        return mediaAssets.Any(m =>
            m.ClassSessionId == classSessionId
            && !m.IsDeleted
            && string.Equals(m.FileType, "image", StringComparison.OrdinalIgnoreCase));
    }

    private static MediaAssetDto MapToDto(MediaAsset media) => new()
    {
        Id = media.Id,
        UploaderId = media.UploaderId,
        ClassId = media.ClassId,
        ClassSessionId = media.ClassSessionId,
        FileUrl = media.FileUrl,
        FileType = media.FileType,
        VideoStatus = media.VideoStatus,
        StatusLabel = "Ready",
        IsReady = true,
        UploadedAt = media.UploadedAt,
        Tags = [],
    };
}
