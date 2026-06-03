using Microsoft.AspNetCore.Http;
using OboxSteam.Application.DTOs.MaterialDTO;

namespace OboxSteam.Application.Interfaces;

public interface IMaterialService
{
    /// <summary>
    /// Upload file (PDF/DOC/Video/Image) to S3 and save Material record to DB.
    /// </summary>
    Task<MaterialResponseDto> UploadMaterialAsync(IFormFile file, UploadMaterialRequestDto request);

    /// <summary>
    /// Get all materials by Module.
    /// </summary>
    Task<List<MaterialResponseDto>> GetMaterialsByModuleAsync(Guid moduleId);

    /// <summary>
    /// Get all materials by Course.
    /// </summary>
    Task<List<MaterialResponseDto>> GetMaterialsByCourseAsync(Guid courseId);

    /// <summary>
    /// Get all materials by Activity.
    /// </summary>
    Task<List<MaterialResponseDto>> GetMaterialsByActivityAsync(Guid activityId);

    /// <summary>
    /// Update material title.
    /// </summary>
    Task<MaterialResponseDto> UpdateMaterialAsync(Guid materialId, UpdateMaterialRequestDto request);

    /// <summary>
    /// Soft-delete material and remove file from S3.
    /// </summary>
    Task DeleteMaterialAsync(Guid materialId);
}
