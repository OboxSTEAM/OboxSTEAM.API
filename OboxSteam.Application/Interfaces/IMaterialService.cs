using Microsoft.AspNetCore.Http;
using OboxSteam.Application.Commons;
using OboxSteam.Application.DTOs.MaterialDTO;
using OboxSteam.Domain.Enums;

namespace OboxSteam.Application.Interfaces;

public interface IMaterialService
{
    /// <summary>
    /// Upload file (PDF/DOC/Video/Image) to S3 and attach to a SelfPaced activity (one per activity).
    /// </summary>
    Task<MaterialResponseDto> UploadMaterialAsync(IFormFile file, UploadMaterialRequestDto request);

    /// <summary>
    /// Get a paginated list of materials with program/course/activity context.
    /// Supports search (title, activity, course, program name), filter, and sort.
    /// </summary>
    Task<Pagination<MaterialListItemDto>> GetAllMaterialsAsync(
        string? search,
        string? sortBy,
        bool isDescending,
        int page,
        int pageSize,
        MaterialType? materialType = null,
        Guid? programId = null,
        Guid? courseId = null,
        Guid? activityId = null);

    /// <summary>
    /// Get the material for a SelfPaced activity, or null if none exists.
    /// </summary>
    Task<MaterialResponseDto?> GetMaterialByActivityAsync(Guid activityId);

    /// <summary>
    /// Get material for an enrolled student with a fresh presigned file URL.
    /// </summary>
    Task<MaterialResponseDto?> GetMaterialByActivityForEnrollmentAsync(Guid activityId, Guid programEnrollmentId);

    /// <summary>
    /// Update material title.
    /// </summary>
    Task<MaterialResponseDto> UpdateMaterialAsync(Guid materialId, UpdateMaterialRequestDto request);

    /// <summary>
    /// Hard-delete material: remove file from S3 first, then delete the database record.
    /// </summary>
    Task DeleteMaterialAsync(Guid materialId);
}
