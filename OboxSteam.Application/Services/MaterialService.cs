using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using OboxSteam.Application.Commons;
using OboxSteam.Application.DTOs.MaterialDTO;
using OboxSteam.Application.Interfaces;
using OboxSteam.Application.Notifications;
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
    private readonly IEnrollmentCurriculumService _enrollmentCurriculumService;
    private readonly INotificationPublisher _notificationPublisher;
    private readonly ILogger<MaterialService> _logger;

    public MaterialService(
        IClaimsService claimsService,
        IUnitOfWork unitOfWork,
        IBlobService blobService,
        IEnrollmentCurriculumService enrollmentCurriculumService,
        INotificationPublisher notificationPublisher,
        ILogger<MaterialService> logger)
    {
        _claimsService = claimsService;
        _unitOfWork    = unitOfWork;
        _blobService   = blobService;
        _enrollmentCurriculumService = enrollmentCurriculumService;
        _notificationPublisher = notificationPublisher;
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
            m => m.ActivityId == request.ActivityId);

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

        await PublishMaterialUpdatedAsync(material, activity!);

        _logger.LogInformation(
            "UploadMaterialAsync completed. MaterialId={MaterialId}, Type={Type}, Size={Size}B",
            material.Id, materialType, file.Length);

        return MapToDto(material);
    }

    // =========================================================================
    // Queries
    // =========================================================================

    /// <inheritdoc />
    public Task<Pagination<MaterialListItemDto>> GetAllMaterialsAsync(
        string? search,
        string? sortBy,
        bool isDescending,
        int page,
        int pageSize,
        MaterialType? materialType = null,
        Guid? programId = null,
        Guid? courseId = null,
        Guid? activityId = null)
    {
        _logger.LogInformation(
            "[GetAllMaterialsAsync] Start — page: {Page}, pageSize: {PageSize}, search: '{Search}'",
            page, pageSize, search);

        var query = BuildMaterialsQuery(
            search, sortBy, isDescending, materialType, programId, courseId, activityId);

        var totalCount = query.Count();

        var items = query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(m => new MaterialListItemDto
            {
                Id = m.Id,
                Title = m.Title,
                MaterialType = m.MaterialType,
                UploadedAt = m.CreatedAt,
                ActivityId = m.ActivityId,
                ActivityName = m.Activity.Name,
                CourseId = m.Activity.CourseId,
                CourseName = m.Activity.Course.Name,
                ProgramId = m.Activity.Course.Module.ProgramId,
                ProgramName = m.Activity.Course.Module.Program.Name,
            })
            .ToList();

        _logger.LogInformation("[GetAllMaterialsAsync] Retrieved {Count}/{Total} materials.", items.Count, totalCount);

        return Task.FromResult(new Pagination<MaterialListItemDto>(items, totalCount, page, pageSize));
    }

    private IQueryable<Material> BuildMaterialsQuery(
        string? search,
        string? sortBy,
        bool isDescending,
        MaterialType? materialType,
        Guid? programId,
        Guid? courseId,
        Guid? activityId)
    {
        var query = _unitOfWork.Materials.GetQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var lowerSearch = search.ToLower();
            query = query.Where(m =>
                m.Title.ToLower().Contains(lowerSearch) ||
                m.Activity.Name.ToLower().Contains(lowerSearch) ||
                m.Activity.Course.Name.ToLower().Contains(lowerSearch) ||
                m.Activity.Course.Module.Program.Name.ToLower().Contains(lowerSearch));
        }

        if (materialType.HasValue)
            query = query.Where(m => m.MaterialType == materialType.Value);

        if (activityId.HasValue)
            query = query.Where(m => m.ActivityId == activityId.Value);

        if (courseId.HasValue)
            query = query.Where(m => m.Activity.CourseId == courseId.Value);

        if (programId.HasValue)
            query = query.Where(m => m.Activity.Course.Module.ProgramId == programId.Value);

        return sortBy?.ToLower() switch
        {
            "title" => isDescending
                ? query.OrderByDescending(m => m.Title)
                : query.OrderBy(m => m.Title),
            "materialtype" => isDescending
                ? query.OrderByDescending(m => m.MaterialType)
                : query.OrderBy(m => m.MaterialType),
            "activityname" => isDescending
                ? query.OrderByDescending(m => m.Activity.Name)
                : query.OrderBy(m => m.Activity.Name),
            "coursename" => isDescending
                ? query.OrderByDescending(m => m.Activity.Course.Name)
                : query.OrderBy(m => m.Activity.Course.Name),
            "programname" => isDescending
                ? query.OrderByDescending(m => m.Activity.Course.Module.Program.Name)
                : query.OrderBy(m => m.Activity.Course.Module.Program.Name),
            "uploadedat" => isDescending
                ? query.OrderByDescending(m => m.CreatedAt)
                : query.OrderBy(m => m.CreatedAt),
            _ => isDescending
                ? query.OrderByDescending(m => m.CreatedAt)
                : query.OrderBy(m => m.CreatedAt),
        };
    }

    /// <inheritdoc />
    public async Task<MaterialResponseDto?> GetMaterialByActivityAsync(Guid activityId)
    {
        _logger.LogInformation("GetMaterialByActivityAsync: ActivityId={ActivityId}", activityId);

        var activity = await _unitOfWork.Activities.GetByIdAsync(activityId);
        MaterialValidator.ValidateActivityExists(activity, activityId);
        MaterialValidator.ValidateSelfPacedOnly(activity!);

        var material = await _unitOfWork.Materials.FirstOrDefaultAsync(
            m => m.ActivityId == activityId);

        return material == null ? null : MapToDto(material);
    }

    /// <inheritdoc />
    public async Task<MaterialResponseDto?> GetMaterialByActivityForEnrollmentAsync(
        Guid activityId,
        Guid programEnrollmentId)
    {
        _logger.LogInformation(
            "GetMaterialByActivityForEnrollmentAsync: ActivityId={ActivityId}, EnrollmentId={EnrollmentId}",
            activityId,
            programEnrollmentId);

        await _enrollmentCurriculumService.EnsureActivityAccessibleAsync(programEnrollmentId, activityId);

        var activity = await _unitOfWork.Activities.GetByIdAsync(activityId);
        MaterialValidator.ValidateActivityExists(activity, activityId);
        MaterialValidator.ValidateSelfPacedOnly(activity!);

        var material = await _unitOfWork.Materials.FirstOrDefaultAsync(
            m => m.ActivityId == activityId);

        if (material == null)
        {
            return null;
        }

        var dto = MapToDto(material);
        dto.FileUrl = await ResolvePresignedFileUrlAsync(material.FileUrl);
        return dto;
    }

    // =========================================================================
    // Update
    // =========================================================================

    /// <inheritdoc />
    public async Task<MaterialResponseDto> UpdateMaterialAsync(Guid materialId, UpdateMaterialRequestDto request)
    {
        _logger.LogInformation("UpdateMaterialAsync: MaterialId={MaterialId}", materialId);

        var material = await _unitOfWork.Materials.GetByIdAsync(materialId);
        if (material == null)
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

        var activity = await _unitOfWork.Activities.GetByIdAsync(material.ActivityId);
        if (activity != null)
        {
            await PublishMaterialUpdatedAsync(material, activity);
        }

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
        if (material == null)
        {
            throw ErrorHelper.NotFound("Material not found.");
        }

        await DeleteMaterialFileFromS3Async(material);

        await _unitOfWork.Materials.HardRemoveRange([material]);
        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation("Material deleted: {MaterialId}", materialId);
    }

    // =========================================================================
    // Private helpers
    // =========================================================================

    /// <summary>
    /// Notifies Open/InProgress class rosters for the material's program.
    /// Skips when no active cohort can be resolved (e.g. SelfPaced-only programs with no class yet).
    /// </summary>
    private async Task PublishMaterialUpdatedAsync(Material material, Activity activity)
    {
        var course = await _unitOfWork.Courses.GetByIdAsync(activity.CourseId);
        if (course == null)
        {
            return;
        }

        var module = await _unitOfWork.Modules.GetByIdAsync(course.ModuleId);
        if (module == null)
        {
            return;
        }

        var activeClasses = await _unitOfWork.Classes.GetAllAsync(
            c => c.ProgramId == module.ProgramId
                 && (c.Status == ClassStatus.Open || c.Status == ClassStatus.InProgress));

        if (activeClasses.Count == 0)
        {
            return;
        }

        var commands = activeClasses
            .Select(c => NotificationCatalog.MaterialUpdated(
                c.Id,
                material.Id,
                activity.Id,
                module.ProgramId,
                material.Title,
                activity.CourseId))
            .ToList();

        await _notificationPublisher.PublishManyAsync(commands);
    }

    private async Task DeleteMaterialFileFromS3Async(Material material)
    {
        if (string.IsNullOrWhiteSpace(material.FileUrl))
        {
            return;
        }

        var s3Key = ExtractS3Key(material.FileUrl, _blobService.BucketName);
        if (string.IsNullOrWhiteSpace(s3Key))
        {
            s3Key = material.FileUrl;
        }

        await _blobService.DeleteByKeyAsync(s3Key);
        _logger.LogInformation("Deleted S3 object: {S3Key}", s3Key);
    }

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

    private async Task<string?> ResolvePresignedFileUrlAsync(string? fileUrl)
    {
        if (string.IsNullOrWhiteSpace(fileUrl))
        {
            return fileUrl;
        }

        var s3Key = ExtractS3Key(fileUrl);
        if (string.IsNullOrWhiteSpace(s3Key))
        {
            return fileUrl;
        }

        return await _blobService.GetFileUrlAsync(s3Key);
    }

    /// <summary>
    /// Resolves the bucket-relative S3 key from a stored file URL or raw key.
    /// Percent-decodes the path so presigned URL generation does not double-encode
    /// seed assets whose <c>FileUrl</c> is a full public S3 link with UTF-8 escapes.
    /// ASCII upload keys (e.g. <c>materials/pdf/{id}.pdf</c>) are unchanged.
    /// </summary>
    private static string? ExtractS3Key(string fileUrl, string? bucketName = null)
    {
        if (string.IsNullOrWhiteSpace(fileUrl))
        {
            return fileUrl;
        }

        if (!fileUrl.StartsWith("http", StringComparison.OrdinalIgnoreCase))
        {
            return fileUrl;
        }

        if (!Uri.TryCreate(fileUrl, UriKind.Absolute, out var uri))
        {
            return fileUrl;
        }

        var path = uri.AbsolutePath.TrimStart('/');

        if (!string.IsNullOrEmpty(bucketName))
        {
            var bucketPrefix = $"{bucketName}/";
            if (path.StartsWith(bucketPrefix, StringComparison.OrdinalIgnoreCase))
            {
                path = path[bucketPrefix.Length..];
            }
        }

        // '+' is space in S3 public URLs; '%XX' must be decoded once before presigning.
        return Uri.UnescapeDataString(path.Replace('+', ' '));
    }

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
