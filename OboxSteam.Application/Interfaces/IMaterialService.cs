using Microsoft.AspNetCore.Http;
using OboxSteam.Application.DTOs.MaterialDTO;

namespace OboxSteam.Application.Interfaces;

public interface IMaterialService
{
    /// <summary>
    /// Upload file (PDF/DOC/Video/Image) to S3 and attach to a SelfPaced activity (one per activity).
    /// </summary>
    Task<MaterialResponseDto> UploadMaterialAsync(IFormFile file, UploadMaterialRequestDto request);

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
    /// Soft-delete material and remove file from S3.
    /// </summary>
    Task DeleteMaterialAsync(Guid materialId);
}
