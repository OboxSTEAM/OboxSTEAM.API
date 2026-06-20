using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using OboxSteam.Application.DTOs.MaterialDTO;
using OboxSteam.Application.Interfaces;
using OboxSteam.Application.Utils;
using OboxSteam.Application.Validation;
using OboxSteam.Domain.Entities;
using OboxSteam.Domain.Enums;
using OboxSteam.Domain.Interfaces;

namespace OboxSteam.Application.Services;

public class MaterialService : IMaterialService
{
    // ── Allowed extensions grouped by type ───────────────────────────────────
    private static readonly HashSet<string> PdfExtensions = new(StringComparer.OrdinalIgnoreCase)
        { ".pdf" };

    private static readonly HashSet<string> DocExtensions = new(StringComparer.OrdinalIgnoreCase)
        { ".doc", ".docx" };

    private static readonly HashSet<string> VideoExtensions = new(StringComparer.OrdinalIgnoreCase)
        { ".mp4", ".mov", ".avi", ".mkv" };

    private static readonly HashSet<string> ImageExtensions = new(StringComparer.OrdinalIgnoreCase)
        { ".jpg", ".jpeg", ".png", ".gif", ".webp" };

    // ── File size limits ──────────────────────────────────────────────────────
    private const long MaxDocSize   = 50L  * 1024 * 1024;          // 50 MB
    private const long MaxImageSize = 10L  * 1024 * 1024;          // 10 MB
    private const long MaxVideoSize = 3L   * 1024 * 1024 * 1024;   // 3  GB

    // ── S3 folder constants ───────────────────────────────────────────────────
    private const string FolderPdf     = "materials/pdf";
    private const string FolderDoc     = "materials/doc";
    private const string FolderVideo   = "materials/video";
    private const string FolderImage   = "materials/image";

    private readonly IClaimsService _claimsService;
    private readonly IUnitOfWork    _unitOfWork;
    private readonly IBlobService   _blobService;
    private readonly ILogger<MaterialService> _logger;

    public MaterialService(
        IClaimsService claimsService,
        IUnitOfWork unitOfWork,
        IBlobService blobService,
        ILogger<MaterialService> logger)
    {
        _claimsService = claimsService;
        _unitOfWork    = unitOfWork;
        _blobService   = blobService;
        _logger        = logger;
    }

    // =========================================================================
    // Upload
    // =========================================================================

    /// <inheritdoc />
    public async Task<MaterialResponseDto> UploadMaterialAsync(IFormFile file, UploadMaterialRequestDto request)
    {
        var userId = _claimsService.GetCurrentUserId;
        _logger.LogInformation(
            "UploadMaterialAsync started by UserId={UserId} for ActivityId={ActivityId}",
            userId, request.ActivityId);

        var extension    = Path.GetExtension(file.FileName).ToLowerInvariant();
        var materialType = ResolveType(extension);

        if (materialType is null)
        {
            throw ErrorHelper.BadRequest(
                "File type not supported. Allowed: PDF (.pdf), DOC (.doc, .docx), " +
                "Video (.mp4, .mov, .avi, .mkv), Image (.jpg, .jpeg, .png, .gif, .webp).");
        }

        ValidateFileSize(materialType.Value, file.Length);

        var activity = await _unitOfWork.Activities.GetByIdAsync(request.ActivityId);
        MaterialValidator.ValidateActivityExists(activity, request.ActivityId);
        MaterialValidator.ValidateSelfPacedOnly(activity!);

        var existing = await _unitOfWork.Materials.FirstOrDefaultAsync(
            m => m.ActivityId == request.ActivityId && !m.IsDeleted);

        if (existing != null)
        {
            throw ErrorHelper.Conflict("This activity already has a material. Delete it before uploading a new one.");
        }

        var folder   = ResolveFolder(materialType.Value);
        var fileName = $"{request.ActivityId}_{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}{extension}";
        var s3Key    = $"{folder}/{fileName}";

        _logger.LogInformation("Uploading material to S3: {S3Key}", s3Key);

        await using var stream = file.OpenReadStream();
        await _blobService.UploadFileAsync(fileName, stream, folder);

        var fileUrl = await _blobService.GetPreviewUrlAsync(s3Key);

        var material = new Material
        {
            ActivityId    = request.ActivityId,
            Title         = request.Title,
            MaterialType  = materialType.Value,
            FileUrl       = fileUrl,
            FileSizeBytes = file.Length,
            CreatedBy     = userId,
            CreatedAt     = DateTime.UtcNow
        };

        await _unitOfWork.Materials.AddAsync(material);
        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation(
            "UploadMaterialAsync completed. MaterialId={MaterialId}, Type={Type}, Size={Size}B",
            material.Id, materialType, file.Length);

        return MapToDto(material);
    }

    // =========================================================================
    // Queries
    // =========================================================================

    /// <inheritdoc />
    public async Task<MaterialResponseDto?> GetMaterialByActivityAsync(Guid activityId)
    {
        _logger.LogInformation("GetMaterialByActivityAsync: ActivityId={ActivityId}", activityId);

        var activity = await _unitOfWork.Activities.GetByIdAsync(activityId);
        MaterialValidator.ValidateActivityExists(activity, activityId);
        MaterialValidator.ValidateSelfPacedOnly(activity!);

        var material = await _unitOfWork.Materials.FirstOrDefaultAsync(
            m => m.ActivityId == activityId && !m.IsDeleted);

        return material == null ? null : MapToDto(material);
    }

    // =========================================================================
    // Update
    // =========================================================================

    /// <inheritdoc />
    public async Task<MaterialResponseDto> UpdateMaterialAsync(Guid materialId, UpdateMaterialRequestDto request)
    {
        _logger.LogInformation("UpdateMaterialAsync: MaterialId={MaterialId}", materialId);

        var material = await _unitOfWork.Materials.GetByIdAsync(materialId);
        if (material == null || material.IsDeleted)
        {
            throw ErrorHelper.NotFound("Material not found.");
        }

        if (!string.IsNullOrWhiteSpace(request.Title))
        {
            material.Title = request.Title;
        }

        material.UpdatedAt = DateTime.UtcNow;
        material.UpdatedBy = _claimsService.GetCurrentUserId;

        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation("UpdateMaterialAsync completed. MaterialId={MaterialId}", materialId);
        return MapToDto(material);
    }

    // =========================================================================
    // Delete
    // =========================================================================

    /// <inheritdoc />
    public async Task DeleteMaterialAsync(Guid materialId)
    {
        _logger.LogInformation("DeleteMaterialAsync: MaterialId={MaterialId}", materialId);

        var material = await _unitOfWork.Materials.GetByIdAsync(materialId);
        if (material == null || material.IsDeleted)
        {
            throw ErrorHelper.NotFound("Material not found.");
        }

        if (!string.IsNullOrWhiteSpace(material.FileUrl))
        {
            try
            {
                string s3Key;
                if (Uri.TryCreate(material.FileUrl, UriKind.Absolute, out var uri))
                {
                    s3Key = uri.AbsolutePath.TrimStart('/');
                    var bucketPrefix = $"{_blobService.BucketName}/";
                    if (s3Key.StartsWith(bucketPrefix, StringComparison.OrdinalIgnoreCase))
                    {
                        s3Key = s3Key[bucketPrefix.Length..];
                    }
                }
                else
                {
                    s3Key = material.FileUrl;
                }

                await _blobService.DeleteByKeyAsync(s3Key);
                _logger.LogInformation("Deleted S3 object: {S3Key}", s3Key);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "Failed to delete S3 file for MaterialId={MaterialId}. Manual cleanup may be needed.",
                    materialId);
            }
        }

        await _unitOfWork.Materials.SoftRemove(material);
        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation("Material deleted (soft): {MaterialId}", materialId);
    }

    // =========================================================================
    // Private helpers
    // =========================================================================

    internal static MaterialResponseDto MapToDto(Material m) => new()
    {
        Id            = m.Id,
        ActivityId    = m.ActivityId,
        Title         = m.Title,
        MaterialType  = m.MaterialType,
        FileUrl       = m.FileUrl,
        FileSizeBytes = m.FileSizeBytes,
        UploaderId    = m.CreatedBy,
        UploadedAt    = m.CreatedAt
    };

    /// <summary>
    /// Returns MaterialType or null if extension is not supported.
    /// </summary>
    private static MaterialType? ResolveType(string extension)
    {
        if (PdfExtensions.Contains(extension))   return MaterialType.PDF;
        if (DocExtensions.Contains(extension))   return MaterialType.DOC;
        if (VideoExtensions.Contains(extension)) return MaterialType.Video;
        if (ImageExtensions.Contains(extension)) return MaterialType.Image;
        return null;
    }

    private static string ResolveFolder(MaterialType materialType) => materialType switch
    {
        MaterialType.PDF   => FolderPdf,
        MaterialType.DOC   => FolderDoc,
        MaterialType.Video => FolderVideo,
        MaterialType.Image => FolderImage,
        _       => throw new InvalidOperationException($"Unknown material type: {materialType}")
    };

    private static void ValidateFileSize(MaterialType materialType, long fileLength)
    {
        var (limit, label) = materialType switch
        {
            MaterialType.PDF   => (MaxDocSize,   "PDF file size must not exceed 50 MB."),
            MaterialType.DOC   => (MaxDocSize,   "DOC file size must not exceed 50 MB."),
            MaterialType.Video => (MaxVideoSize, "Video file size must not exceed 3 GB."),
            MaterialType.Image => (MaxImageSize, "Image file size must not exceed 10 MB."),
            _       => (MaxDocSize,   "File size limit exceeded.")
        };

        if (fileLength > limit)
        {
            throw ErrorHelper.BadRequest(label);
        }
    }
}
