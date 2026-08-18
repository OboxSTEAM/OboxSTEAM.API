using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OboxSteam.Application.Commons;
using OboxSteam.Application.DTOs.MaterialDTO;
using OboxSteam.Application.Interfaces;
using OboxSteam.Application.Utils;
using OboxSteam.Domain.Enums;
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
    // GET ALL  —  GET /api/materials
    // =========================================================================

    /// <summary>
    /// Get a paginated list of materials with program/course/activity context.
    /// </summary>
    [HttpGet]
    [SwaggerOperation(
        Summary = "Get all materials",
        Description = "Retrieve a paginated list of materials, each carrying program/course/activity " +
                      "context for the Edit deep-link. Supports search, filter, and sort options.")]
    [ProducesResponseType(typeof(ApiResult<Pagination<MaterialListItemDto>>), 200)]
    [ProducesResponseType(typeof(ApiResult<object>), 400)]
    [ProducesResponseType(typeof(ApiResult<object>), 500)]
    public async Task<IActionResult> GetAllMaterials(
        [FromQuery, SwaggerParameter(Description = "Search by material title, activity, course, or program name (optional)")] string? search = null,
        [FromQuery, SwaggerParameter(Description = "Sort by field: title, materialType, uploadedAt, activityName, courseName, programName (optional)")] string? sortBy = null,
        [FromQuery, SwaggerParameter(Description = "Sort in descending order? Default: false")] bool isDescending = false,
        [FromQuery, SwaggerParameter(Description = "Page number, starting from 1")] int page = 1,
        [FromQuery, SwaggerParameter(Description = "Number of items per page")] int pageSize = 10,
        [FromQuery, SwaggerParameter(Description = "Filter by material type (optional)")] MaterialType? materialType = null,
        [FromQuery, SwaggerParameter(Description = "Filter by program (optional)")] Guid? programId = null,
        [FromQuery, SwaggerParameter(Description = "Filter by course (optional)")] Guid? courseId = null,
        [FromQuery, SwaggerParameter(Description = "Filter by activity (optional)")] Guid? activityId = null)
    {
        if (page < 1 || pageSize < 1)
            return BadRequest(ApiResult<object>.Failure("400", "Invalid pagination parameters."));

        var result = await _materialService.GetAllMaterialsAsync(
            search, sortBy, isDescending, page, pageSize,
            materialType, programId, courseId, activityId);

        return Ok(ApiResult<Pagination<MaterialListItemDto>>.Success(result, "200", "Materials retrieved successfully."));
    }

    // =========================================================================
    // UPLOAD  —  POST /api/materials/upload
    // =========================================================================

    /// <summary>
    /// Upload a learning material for a SelfPaced activity (one material per activity).
    /// </summary>
    [HttpPost("upload")]
    [RequestSizeLimit(3L * 1024 * 1024 * 1024)]
    [RequestFormLimits(MultipartBodyLengthLimit = 3L * 1024 * 1024 * 1024)]
    [SwaggerOperation(
        Summary = "Upload material",
        Description = "Uploads learning material to S3 for a SelfPaced activity. Supported formats: " +
                      "PDF (≤50 MB), DOC/DOCX (≤50 MB), Video .mp4/.mov/.avi/.mkv (≤3 GB), " +
                      "Image .jpg/.jpeg/.png/.gif/.webp (≤10 MB). ActivityId is required."
    )]
    [ProducesResponseType(typeof(ApiResult<MaterialResponseDto>), 200)]
    [ProducesResponseType(typeof(ApiResult<object>), 400)]
    [ProducesResponseType(typeof(ApiResult<object>), 404)]
    [ProducesResponseType(typeof(ApiResult<object>), 409)]
    public async Task<IActionResult> UploadMaterial(
        IFormFile file,
        [FromQuery] Guid activityId,
        [FromQuery, SwaggerParameter("Display title of the material")] string title = "")
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            return BadRequest(ApiResult<object>.Failure("400", "Title is required."));
        }

        var request = new UploadMaterialRequestDto
        {
            ActivityId = activityId,
            Title      = title
        };

        var result = await _materialService.UploadMaterialAsync(file, request);
        return Ok(ApiResult<MaterialResponseDto>.Success(result, "200", "Material uploaded successfully."));
    }

    // =========================================================================
    // GET BY ACTIVITY  —  GET /api/materials/activity/{activityId}
    // =========================================================================

    /// <summary>
    /// Get the material for a SelfPaced activity.
    /// </summary>
    [HttpGet("activity/{activityId:guid}")]
    [Authorize(Roles = "Student,Parent,Admin,Manager")]
    [SwaggerOperation(
        Summary = "Get material by activity",
        Description = "Returns the learning material for a SelfPaced activity. Students must pass programEnrollmentId for enrollment-scoped access.")]
    [ProducesResponseType(typeof(ApiResult<MaterialResponseDto>), 200)]
    [ProducesResponseType(typeof(ApiResult<object>), 400)]
    [ProducesResponseType(typeof(ApiResult<object>), 403)]
    [ProducesResponseType(typeof(ApiResult<object>), 404)]
    public async Task<IActionResult> GetMaterialByActivity(
        [FromRoute] Guid activityId,
        [FromQuery, SwaggerParameter(Description = "Required for students — scopes access to an active enrollment")] Guid? programEnrollmentId = null)
    {
        MaterialResponseDto? result;

        if (User.IsInRole("Student"))
        {
            if (!programEnrollmentId.HasValue)
            {
                return BadRequest(ApiResult<object>.Failure(
                    "400",
                    "programEnrollmentId is required for student access."));
            }

            result = await _materialService.GetMaterialByActivityForEnrollmentAsync(
                activityId,
                programEnrollmentId.Value);
        }
        else if (programEnrollmentId.HasValue)
        {
            result = await _materialService.GetMaterialByActivityForEnrollmentAsync(
                activityId,
                programEnrollmentId.Value);
        }
        else
        {
            result = await _materialService.GetMaterialByActivityAsync(activityId);
        }

        if (result == null)
        {
            return NotFound(ApiResult<object>.Failure("404", "Material not found for this activity."));
        }

        return Ok(ApiResult<MaterialResponseDto>.Success(result, "200", "Material retrieved successfully."));
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
    /// Hard-delete material: remove file from S3 first, then delete the database record.
    /// </summary>
    [HttpDelete("{materialId:guid}")]
    [SwaggerOperation(
        Summary = "Delete material",
        Description = "Deletes the material file from S3, then removes the database record."
    )]
    [ProducesResponseType(typeof(ApiResult), 200)]
    [ProducesResponseType(typeof(ApiResult<object>), 404)]
    public async Task<IActionResult> DeleteMaterial([FromRoute] Guid materialId)
    {
        await _materialService.DeleteMaterialAsync(materialId);
        return Ok(ApiResult.Success("200", "Material deleted successfully."));
    }
}
