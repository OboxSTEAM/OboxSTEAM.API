using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OboxSteam.Application.Commons;
using OboxSteam.Application.DTOs.ActivityProgressDTO;
using OboxSteam.Application.Interfaces;
using OboxSteam.Application.Utils;
using Swashbuckle.AspNetCore.Annotations;

namespace OboxSteam.API.Controllers;

[Route("api/activity-progresses")]
[ApiController]
public class ActivityProgressController : ControllerBase
{
    private readonly IActivityProgressService _activityProgressService;

    public ActivityProgressController(IActivityProgressService activityProgressService)
    {
        _activityProgressService = activityProgressService;
    }

    // =========================================================================
    // START  —  POST /api/activity-progresses          [Student only]
    // =========================================================================

    [HttpPost]
    [Authorize(Roles = "Student")]
    [SwaggerOperation(
        Summary = "Start activity progress",
        Description = "Creates activity progress for a module enrollment and sets status to InProgress. Requires Student role.")]
    [ProducesResponseType(typeof(ApiResult<ActivityProgressResponseDto>), 201)]
    [ProducesResponseType(typeof(ApiResult<object>), 400)]
    [ProducesResponseType(typeof(ApiResult<object>), 401)]
    [ProducesResponseType(typeof(ApiResult<object>), 403)]
    [ProducesResponseType(typeof(ApiResult<object>), 404)]
    [ProducesResponseType(typeof(ApiResult<object>), 409)]
    public async Task<IActionResult> StartActivityProgress(
        [FromBody, SwaggerParameter("Start activity progress request")] CreateActivityProgressRequestDto dto)
    {
        var result = await _activityProgressService.StartActivityProgressAsync(dto);

        return Created(
            $"/api/activity-progresses/{result.Id}",
            ApiResult<ActivityProgressResponseDto>.Success(result, "201", "Activity progress started successfully."));
    }

    // =========================================================================
    // DONE  —  PATCH /api/activity-progresses/done   [Student only]
    // =========================================================================

    [HttpPatch("done")]
    [Authorize(Roles = "Student")]
    [SwaggerOperation(
        Summary = "Mark activity as done",
        Description = "Marks an activity as Done for the given module enrollment and recalculates module/program progress. Requires Student role.")]
    [ProducesResponseType(typeof(ApiResult<ActivityProgressResponseDto>), 200)]
    [ProducesResponseType(typeof(ApiResult<object>), 400)]
    [ProducesResponseType(typeof(ApiResult<object>), 401)]
    [ProducesResponseType(typeof(ApiResult<object>), 403)]
    [ProducesResponseType(typeof(ApiResult<object>), 404)]
    public async Task<IActionResult> CompleteActivityProgress(
        [FromBody, SwaggerParameter("Complete activity progress request")] UpdateActivityProgressRequestDto dto)
    {
        var result = await _activityProgressService.UpdateActivityProgressAsync(dto);

        return Ok(ApiResult<ActivityProgressResponseDto>.Success(
            result,
            "200",
            "Activity marked as done successfully."));
    }

    // =========================================================================
    // FORCE COMPLETE (TEST)  —  POST /api/activity-progresses/force-complete
    // [Mentor, Manager]
    // =========================================================================

    [HttpPost("force-complete")]
    [Authorize(Roles = "Mentor,Manager")]
    [SwaggerOperation(
        Summary = "Force an activity to Done (test only)",
        Description = "Test-only endpoint that forces any activity to Done for a given student, bypassing all "
            + "business rules (enrollment status, ownership, activity type, sequential lock, schedule windows). "
            + "The module enrollment is resolved automatically. Requires Mentor or Manager role.")]
    [ProducesResponseType(typeof(ApiResult<ActivityProgressResponseDto>), 200)]
    [ProducesResponseType(typeof(ApiResult<object>), 400)]
    [ProducesResponseType(typeof(ApiResult<object>), 401)]
    [ProducesResponseType(typeof(ApiResult<object>), 403)]
    [ProducesResponseType(typeof(ApiResult<object>), 404)]
    public async Task<IActionResult> ForceCompleteActivity(
        [FromBody, SwaggerParameter("Force complete activity request")] ForceCompleteActivityRequestDto dto)
    {
        var result = await _activityProgressService.ForceCompleteActivityAsync(dto.StudentId, dto.ActivityId);

        return Ok(ApiResult<ActivityProgressResponseDto>.Success(
            result,
            "200",
            "Activity force-completed successfully."));
    }
}
