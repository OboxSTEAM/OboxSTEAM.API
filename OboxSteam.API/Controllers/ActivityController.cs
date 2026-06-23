using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OboxSteam.Application.Commons;
using OboxSteam.Application.DTOs.ActivityDTO;
using OboxSteam.Application.Interfaces;
using OboxSteam.Application.Utils;
using OboxSteam.Domain.Enums;
using Swashbuckle.AspNetCore.Annotations;

namespace OboxSteam.API.Controllers;

[Route("api/activities")]
[ApiController]
public class ActivityController : ControllerBase
{
    private readonly IActivityService _activityService;
    private readonly IEnrollmentCurriculumService _enrollmentCurriculumService;

    public ActivityController(
        IActivityService activityService,
        IEnrollmentCurriculumService enrollmentCurriculumService)
    {
        _activityService = activityService;
        _enrollmentCurriculumService = enrollmentCurriculumService;
    }

    // =========================================================================
    // GET ALL  —  GET /api/activities
    // =========================================================================

    [HttpGet]
    [SwaggerOperation(
        Summary = "Get all activities",
        Description = "Retrieve a paginated list of activities with optional search, filter, and sort options.")]
    [ProducesResponseType(typeof(ApiResult<Pagination<ActivitiesResponseDto>>), 200)]
    [ProducesResponseType(typeof(ApiResult<object>), 400)]
    public async Task<IActionResult> GetAllActivities(
        [FromQuery, SwaggerParameter(Description = "Search by name or code (optional)")] string? search = null,
        [FromQuery, SwaggerParameter(Description = "Sort by field: name, code, activityOrder, activityType, startTime, endTime, createdAt (optional)")] string? sortBy = null,
        [FromQuery, SwaggerParameter(Description = "Sort in descending order? Default: false")] bool isDescending = false,
        [FromQuery, SwaggerParameter(Description = "Page number, starting from 1")] int page = 1,
        [FromQuery, SwaggerParameter(Description = "Number of items per page")] int pageSize = 10,
        [FromQuery, SwaggerParameter(Description = "Filter by activity code (optional)")] string? code = null,
        [FromQuery, SwaggerParameter(Description = "Filter by activity type (optional)")] ActivityType? activityType = null)
    {
        if (page < 1 || pageSize < 1)
        {
            return BadRequest(ApiResult<object>.Failure("400", "Invalid pagination parameters."));
        }

        var result = await _activityService.GetAllActivitiesAsync(

            search, sortBy, isDescending, page, pageSize, code, activityType);

        return Ok(ApiResult<Pagination<ActivitiesResponseDto>>.Success(result, "200", "Activities retrieved successfully."));
    }

    // =========================================================================
    // GET BY ID  —  GET /api/activities/{id}
    // =========================================================================

    [HttpGet("{id:guid}")]
    [SwaggerOperation(
        Summary = "Get activity details",
        Description = "Retrieve detailed information for a specific activity by its ID. Students must pass programEnrollmentId.")]
    [ProducesResponseType(typeof(ApiResult<ActivitiesResponseDto>), 200)]
    [ProducesResponseType(typeof(ApiResult<object>), 400)]
    [ProducesResponseType(typeof(ApiResult<object>), 403)]
    [ProducesResponseType(typeof(ApiResult<object>), 404)]
    public async Task<IActionResult> GetActivityById(
        [FromRoute] Guid id,
        [FromQuery, SwaggerParameter(Description = "Required for students — scopes access to an active enrollment")] Guid? programEnrollmentId = null)
    {
        if (User.IsInRole("Student"))
        {
            if (!programEnrollmentId.HasValue)
            {
                return BadRequest(ApiResult<object>.Failure(
                    "400",
                    "programEnrollmentId is required for student access."));
            }

            await _enrollmentCurriculumService.EnsureActivityAccessibleAsync(programEnrollmentId.Value, id);
        }
        else if (programEnrollmentId.HasValue)
        {
            await _enrollmentCurriculumService.EnsureActivityAccessibleAsync(programEnrollmentId.Value, id);
        }

        var result = await _activityService.GetActivityByIdAsync(id);

        if (result == null)
        {
            return NotFound(ApiResult<object>.Failure("404", $"Activity with ID '{id}' not found."));
        }

        if (User.IsInRole("Student") && result.Material != null)
        {
            result.Material.FileUrl = null;
        }

        if (programEnrollmentId.HasValue)
        {
            result.LearningProgress = await _enrollmentCurriculumService.GetActivityLearningProgressAsync(
                programEnrollmentId.Value,
                id);
        }

        return Ok(ApiResult<ActivitiesResponseDto>.Success(result, "200", "Activity retrieved successfully."));
    }


    // =========================================================================
    // CREATE  —  POST /api/activities          [Admin only]
    // =========================================================================

    [HttpPost]
    [Authorize(Roles = "SuperAdmin,Manager")]
    [SwaggerOperation(
        Summary = "Create a new activity",
        Description = "Creates a new activity with the provided information. Requires SuperAdmin or Manager role.")]
    [ProducesResponseType(typeof(ApiResult<ActivitiesResponseDto>), 201)]
    [ProducesResponseType(typeof(ApiResult<object>), 400)]
    [ProducesResponseType(typeof(ApiResult<object>), 401)]
    [ProducesResponseType(typeof(ApiResult<object>), 403)]
    [ProducesResponseType(typeof(ApiResult<object>), 409)]
    public async Task<IActionResult> CreateActivity(
        [FromBody, SwaggerParameter("New activity data to be created")] CreateActivitiesRequestDto dto)
    {
        var result = await _activityService.CreateActivityAsync(dto);

        return CreatedAtAction(
            nameof(GetActivityById),
            new { id = result.Id },
            ApiResult<ActivitiesResponseDto>.Success(result, "201", "Activity created successfully."));
    }

    // =========================================================================
    // UPDATE  —  PUT /api/activities/{id}      [Admin only]
    // =========================================================================

    [HttpPut("{id:guid}")]
    [Authorize(Roles = "SuperAdmin,Manager")]
    [SwaggerOperation(
        Summary = "Update activity information",
        Description = "Updates the details of a specific activity by its ID. Requires SuperAdmin or Manager role.")]
    [ProducesResponseType(typeof(ApiResult<ActivitiesResponseDto>), 200)]
    [ProducesResponseType(typeof(ApiResult<object>), 400)]
    [ProducesResponseType(typeof(ApiResult<object>), 401)]
    [ProducesResponseType(typeof(ApiResult<object>), 403)]
    [ProducesResponseType(typeof(ApiResult<object>), 404)]
    [ProducesResponseType(typeof(ApiResult<object>), 409)]
    public async Task<IActionResult> UpdateActivity(
        [FromRoute] Guid id,
        [FromBody, SwaggerParameter("Updated activity data")] UpdateActivitiesRequestDto dto)
    {
        if (dto == null)
        {
            return BadRequest(ApiResult<object>.Failure("400", "Activity update data is required."));
        }

        var result = await _activityService.UpdateActivityAsync(id, dto);

        if (result == null)
        {
            return NotFound(ApiResult<object>.Failure("404", $"Activity with ID '{id}' not found."));
        }

        return Ok(ApiResult<ActivitiesResponseDto>.Success(result, "200", "Activity updated successfully."));
    }

    // =========================================================================
    // DELETE  —  DELETE /api/activities/{id}   [Admin only]
    // =========================================================================

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "SuperAdmin,Manager")]
    [SwaggerOperation(
        Summary = "Delete an activity",
        Description = "Soft-deletes an activity by its ID. Requires SuperAdmin or Manager role.")]
    [ProducesResponseType(typeof(ApiResult<bool>), 200)]
    [ProducesResponseType(typeof(ApiResult<object>), 401)]
    [ProducesResponseType(typeof(ApiResult<object>), 403)]
    [ProducesResponseType(typeof(ApiResult<object>), 404)]
    public async Task<IActionResult> DeleteActivity([FromRoute] Guid id)
    {
        var result = await _activityService.DeleteActivityAsync(id);

        if (!result)
        {
            return NotFound(ApiResult<object>.Failure("404", $"Activity with ID '{id}' not found."));
        }

        return Ok(ApiResult<bool>.Success(result, "200", "Activity deleted successfully."));
    }
}
