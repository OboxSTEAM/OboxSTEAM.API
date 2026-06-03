using Microsoft.AspNetCore.Mvc;
using OboxSteam.Application.DTOs.MaterialDTO;
using OboxSteam.Application.Interfaces;
using OboxSteam.Application.Utils;
using Swashbuckle.AspNetCore.Annotations;

namespace OboxSteam.API.Controllers;

[Route("api/materials")]
[ApiController]
public class MaterialController : ControllerBase
{
    private readonly IMaterialService _materialService;

    public MaterialController(IMaterialService materialService)
    {
        _materialService = materialService;
    }

    // =========================================================================
    // UPLOAD  —  POST /api/materials/upload
    // =========================================================================

    /// <summary>
    /// Upload a learning material (PDF, DOC, Video, Image) for a Module/Course/Activity.
    /// </summary>
    [HttpPost("upload")]
    [RequestSizeLimit(3L * 1024 * 1024 * 1024)]
    [RequestFormLimits(MultipartBodyLengthLimit = 3L * 1024 * 1024 * 1024)]
    [SwaggerOperation(
        Summary = "Upload material",
        Description = "Uploads learning material to S3. Supported formats: PDF (≤50 MB), DOC/DOCX (≤50 MB), " +
                      "Video .mp4/.mov/.avi/.mkv (≤3 GB), Image .jpg/.jpeg/.png/.gif/.webp (≤10 MB). " +
                      "ModuleId is required; CourseId and ActivityId are optional."
    )]
    [ProducesResponseType(typeof(ApiResult<MaterialResponseDto>), 200)]
    [ProducesResponseType(typeof(ApiResult<object>), 400)]
    [ProducesResponseType(typeof(ApiResult<object>), 404)]
    public async Task<IActionResult> UploadMaterial(
        IFormFile file,
        [FromQuery] Guid moduleId,
        [FromQuery] Guid? courseId = null,
        [FromQuery] Guid? activityId = null,
        [FromQuery, SwaggerParameter("Display title of the material")] string title = "")
    {
        if (string.IsNullOrWhiteSpace(title))
            return BadRequest(ApiResult<object>.Failure("400", "Title is required."));

        var request = new UploadMaterialRequestDto
        {
            ModuleId   = moduleId,
            CourseId   = courseId,
            ActivityId = activityId,
            Title      = title
        };

        var result = await _materialService.UploadMaterialAsync(file, request);
        return Ok(ApiResult<MaterialResponseDto>.Success(result, "200", "Material uploaded successfully."));
    }

    // =========================================================================
    // GET BY MODULE  —  GET /api/materials/module/{moduleId}
    // =========================================================================

    /// <summary>
    /// Get all materials by Module.
    /// </summary>
    [HttpGet("module/{moduleId:guid}")]
    [SwaggerOperation(
        Summary = "Get materials by module",
        Description = "Get a list of all learning materials belonging to a Module."
    )]
    [ProducesResponseType(typeof(ApiResult<List<MaterialResponseDto>>), 200)]
    [ProducesResponseType(typeof(ApiResult<object>), 404)]
    public async Task<IActionResult> GetMaterialsByModule([FromRoute] Guid moduleId)
    {
        var result = await _materialService.GetMaterialsByModuleAsync(moduleId);
        return Ok(ApiResult<List<MaterialResponseDto>>.Success(result, "200", "Materials retrieved successfully."));
    }

    // =========================================================================
    // GET BY COURSE  —  GET /api/materials/course/{courseId}
    // =========================================================================

    /// <summary>
    /// Get all materials by Course.
    /// </summary>
    [HttpGet("course/{courseId:guid}")]
    [SwaggerOperation(
        Summary = "Get materials by course",
        Description = "Get a list of all learning materials belonging to a Course."
    )]
    [ProducesResponseType(typeof(ApiResult<List<MaterialResponseDto>>), 200)]
    public async Task<IActionResult> GetMaterialsByCourse([FromRoute] Guid courseId)
    {
        var result = await _materialService.GetMaterialsByCourseAsync(courseId);
        return Ok(ApiResult<List<MaterialResponseDto>>.Success(result, "200", "Materials retrieved successfully."));
    }

    // =========================================================================
    // GET BY ACTIVITY  —  GET /api/materials/activity/{activityId}
    // =========================================================================

    /// <summary>
    /// Get all materials by Activity.
    /// </summary>
    [HttpGet("activity/{activityId:guid}")]
    [SwaggerOperation(
        Summary = "Get materials by activity",
        Description = "Get a list of all learning materials belonging to an Activity."
    )]
    [ProducesResponseType(typeof(ApiResult<List<MaterialResponseDto>>), 200)]
    public async Task<IActionResult> GetMaterialsByActivity([FromRoute] Guid activityId)
    {
        var result = await _materialService.GetMaterialsByActivityAsync(activityId);
        return Ok(ApiResult<List<MaterialResponseDto>>.Success(result, "200", "Materials retrieved successfully."));
    }

    // =========================================================================
    // UPDATE  —  PUT /api/materials/{materialId}
    // =========================================================================

    /// <summary>
    /// Update material title.
    /// </summary>
    [HttpPut("{materialId:guid}")]
    [SwaggerOperation(
        Summary = "Update material",
        Description = "Updates the title of a material."
    )]
    [ProducesResponseType(typeof(ApiResult<MaterialResponseDto>), 200)]
    [ProducesResponseType(typeof(ApiResult<object>), 404)]
    public async Task<IActionResult> UpdateMaterial(
        [FromRoute] Guid materialId,
        [FromBody] UpdateMaterialRequestDto dto)
    {
        var result = await _materialService.UpdateMaterialAsync(materialId, dto);
        return Ok(ApiResult<MaterialResponseDto>.Success(result, "200", "Material updated successfully."));
    }

    // =========================================================================
    // DELETE  —  DELETE /api/materials/{materialId}
    // =========================================================================

    /// <summary>
    /// Delete material (soft delete + delete file from S3).
    /// </summary>
    [HttpDelete("{materialId:guid}")]
    [SwaggerOperation(
        Summary = "Delete material",
        Description = "Soft-deletes the material and removes the corresponding file from S3."
    )]
    [ProducesResponseType(typeof(ApiResult), 200)]
    [ProducesResponseType(typeof(ApiResult<object>), 404)]
    public async Task<IActionResult> DeleteMaterial([FromRoute] Guid materialId)
    {
        await _materialService.DeleteMaterialAsync(materialId);
        return Ok(ApiResult.Success("200", "Material deleted successfully."));
    }
}
