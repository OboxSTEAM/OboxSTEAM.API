using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OboxSteam.Application.Commons;
using OboxSteam.Application.DTOs.ClassSessionDTO;
using OboxSteam.Application.Interfaces;
using OboxSteam.Application.Utils;
using OboxSteam.Domain.Enums;
using Swashbuckle.AspNetCore.Annotations;

namespace OboxSteam.API.Controllers;

[Route("api/classes/{classId:guid}/sessions")]
[ApiController]
public class ClassSessionController : ControllerBase
{
    private readonly IClassSessionService _classSessionService;

    public ClassSessionController(IClassSessionService classSessionService)
    {
        _classSessionService = classSessionService;
    }

    // =========================================================================
    // GET ALL  —  GET /api/classes/{classId}/sessions
    // =========================================================================

    [HttpGet]
    [SwaggerOperation(
        Summary = "Get class sessions",
        Description = "Retrieve a paginated list of scheduled sessions for a class cohort.")]
    [ProducesResponseType(typeof(ApiResult<Pagination<ClassSessionResponseDto>>), 200)]
    [ProducesResponseType(typeof(ApiResult<object>), 400)]
    [ProducesResponseType(typeof(ApiResult<object>), 404)]
    public async Task<IActionResult> GetClassSessions(
        [FromRoute] Guid classId,
        [FromQuery, SwaggerParameter(Description = "Sort by: title, startTime, endTime, sessionKind, status, createdAt")] string? sortBy = null,
        [FromQuery, SwaggerParameter(Description = "Sort in descending order? Default: false")] bool isDescending = false,
        [FromQuery, SwaggerParameter(Description = "Page number, starting from 1")] int page = 1,
        [FromQuery, SwaggerParameter(Description = "Number of items per page")] int pageSize = 10,
        [FromQuery, SwaggerParameter(Description = "Filter by module ID (optional)")] Guid? moduleId = null,
        [FromQuery, SwaggerParameter(Description = "Filter by session kind (optional)")] SessionKind? sessionKind = null,
        [FromQuery, SwaggerParameter(Description = "Filter by session status (optional)")] ClassSessionStatus? status = null,
        [FromQuery, SwaggerParameter(Description = "Include sessions ending on or after this time (optional)")] DateTime? from = null,
        [FromQuery, SwaggerParameter(Description = "Include sessions starting on or before this time (optional)")] DateTime? to = null)
    {
        if (page < 1 || pageSize < 1)
        {
            return BadRequest(ApiResult<object>.Failure("400", "Invalid pagination parameters."));
        }

        var result = await _classSessionService.GetClassSessionsByClassIdAsync(
            classId,
            sortBy,
            isDescending,
            page,
            pageSize,
            moduleId,
            sessionKind,
            status,
            from,
            to);

        return Ok(ApiResult<Pagination<ClassSessionResponseDto>>.Success(
            result,
            "200",
            "Class sessions retrieved successfully."));
    }

    // =========================================================================
    // GET BY ID  —  GET /api/classes/{classId}/sessions/{id}
    // =========================================================================

    [HttpGet("{id:guid}")]
    [SwaggerOperation(
        Summary = "Get class session details",
        Description = "Retrieve detailed information for a specific class session.")]
    [ProducesResponseType(typeof(ApiResult<ClassSessionResponseDto>), 200)]
    [ProducesResponseType(typeof(ApiResult<object>), 404)]
    public async Task<IActionResult> GetClassSessionById(
        [FromRoute] Guid classId,
        [FromRoute] Guid id)
    {
        var result = await _classSessionService.GetClassSessionByIdAsync(id);

        if (result.ClassId != classId)
        {
            return NotFound(ApiResult<object>.Failure("404", $"Class session with ID '{id}' not found."));
        }

        return Ok(ApiResult<ClassSessionResponseDto>.Success(result, "200", "Class session retrieved successfully."));
    }

    // =========================================================================
    // CREATE  —  POST /api/classes/{classId}/sessions          [Admin only]
    // =========================================================================

    [HttpPost]
    [Authorize(Roles = "SuperAdmin,Manager")]
    [SwaggerOperation(
        Summary = "Create a class session",
        Description = "Schedules a new session for a class cohort. Requires SuperAdmin or Manager role.")]
    [ProducesResponseType(typeof(ApiResult<ClassSessionResponseDto>), 201)]
    [ProducesResponseType(typeof(ApiResult<object>), 400)]
    [ProducesResponseType(typeof(ApiResult<object>), 401)]
    [ProducesResponseType(typeof(ApiResult<object>), 403)]
    [ProducesResponseType(typeof(ApiResult<object>), 404)]
    public async Task<IActionResult> CreateClassSession(
        [FromRoute] Guid classId,
        [FromBody, SwaggerParameter("New class session data")] CreateClassSessionRequestDto dto)
    {
        if (dto == null)
        {
            return BadRequest(ApiResult<object>.Failure("400", "Class session data is required."));
        }

        dto.ClassId = classId;

        var result = await _classSessionService.CreateClassSessionAsync(dto);

        return CreatedAtAction(
            nameof(GetClassSessionById),
            new { classId, id = result.Id },
            ApiResult<ClassSessionResponseDto>.Success(result, "201", "Class session created successfully."));
    }

    // =========================================================================
    // UPDATE  —  PUT /api/classes/{classId}/sessions/{id}      [Admin only]
    // =========================================================================

    [HttpPut("{id:guid}")]
    [Authorize(Roles = "SuperAdmin,Manager")]
    [SwaggerOperation(
        Summary = "Update a class session",
        Description = "Updates a scheduled class session. Requires SuperAdmin or Manager role.")]
    [ProducesResponseType(typeof(ApiResult<ClassSessionResponseDto>), 200)]
    [ProducesResponseType(typeof(ApiResult<object>), 400)]
    [ProducesResponseType(typeof(ApiResult<object>), 401)]
    [ProducesResponseType(typeof(ApiResult<object>), 403)]
    [ProducesResponseType(typeof(ApiResult<object>), 404)]
    public async Task<IActionResult> UpdateClassSession(
        [FromRoute] Guid classId,
        [FromRoute] Guid id,
        [FromBody, SwaggerParameter("Updated class session data")] UpdateClassSessionRequestDto dto)
    {
        if (dto == null)
        {
            return BadRequest(ApiResult<object>.Failure("400", "Class session update data is required."));
        }

        var existing = await _classSessionService.GetClassSessionByIdAsync(id);

        if (existing.ClassId != classId)
        {
            return NotFound(ApiResult<object>.Failure("404", $"Class session with ID '{id}' not found."));
        }

        var result = await _classSessionService.UpdateClassSessionAsync(id, dto);

        return Ok(ApiResult<ClassSessionResponseDto>.Success(result, "200", "Class session updated successfully."));
    }

    // =========================================================================
    // DELETE  —  DELETE /api/classes/{classId}/sessions/{id}   [Admin only]
    // =========================================================================

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "SuperAdmin,Manager")]
    [SwaggerOperation(
        Summary = "Delete a class session",
        Description = "Soft-deletes a class session. Requires SuperAdmin or Manager role.")]
    [ProducesResponseType(typeof(ApiResult<bool>), 200)]
    [ProducesResponseType(typeof(ApiResult<object>), 401)]
    [ProducesResponseType(typeof(ApiResult<object>), 403)]
    [ProducesResponseType(typeof(ApiResult<object>), 404)]
    public async Task<IActionResult> DeleteClassSession(
        [FromRoute] Guid classId,
        [FromRoute] Guid id)
    {
        var existing = await _classSessionService.GetClassSessionByIdAsync(id);

        if (existing.ClassId != classId)
        {
            return NotFound(ApiResult<object>.Failure("404", $"Class session with ID '{id}' not found."));
        }

        var result = await _classSessionService.DeleteClassSessionAsync(id);

        if (!result)
        {
            return NotFound(ApiResult<object>.Failure("404", $"Class session with ID '{id}' not found."));
        }

        return Ok(ApiResult<bool>.Success(result, "200", "Class session deleted successfully."));
    }
}
