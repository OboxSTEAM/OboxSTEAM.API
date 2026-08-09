using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OboxSteam.Application.DTOs.MediaDTO;
using OboxSteam.Application.Interfaces;
using OboxSteam.Application.Utils;
using Swashbuckle.AspNetCore.Annotations;

namespace OboxSteam.API.Controllers;

/// <summary>
/// Manages personal highlight video stacks for a student within a Class.
/// </summary>
[Route("api/highlight-video")]
[ApiController]
[Authorize(Roles = "Student,Mentor,Manager,Admin")]
public class HighlightVideoController : ControllerBase
{
    private readonly IPersonalVideoService _personalVideoService;

    public HighlightVideoController(IPersonalVideoService personalVideoService)
    {
        _personalVideoService = personalVideoService;
    }

    /// <summary>
    /// Creates a highlight stack (max 3 per student/class) and enqueues the first video.
    /// </summary>
    [HttpPost("stacks")]
    [SwaggerOperation(
        Summary = "Create highlight video stack",
        Description = "Creates a highlight stack for a student in a class and starts the first video job. " +
                      "If StudentId is omitted, the authenticated user from the JWT is used. " +
                      "Students may only create for themselves; staff may pass studentId."
    )]
    [ProducesResponseType(typeof(ApiResult<HighlightVideoStackDto>), 202)]
    [ProducesResponseType(typeof(ApiResult<object>), 400)]
    [ProducesResponseType(typeof(ApiResult<object>), 401)]
    [ProducesResponseType(typeof(ApiResult<object>), 403)]
    [ProducesResponseType(typeof(ApiResult<object>), 404)]
    [ProducesResponseType(typeof(ApiResult<object>), 409)]
    public async Task<IActionResult> CreateStack([FromBody] CreateHighlightStackRequest request)
    {
        var result = await _personalVideoService.CreateStackAsync(
            request.ClassId, request.StudentId, request.StrengthDescription);
        return Accepted(ApiResult<HighlightVideoStackDto>.Success(
            result, "202", "Highlight stack created; video generation started."));
    }

    /// <summary>
    /// Lists all highlight stacks for a student in a class.
    /// </summary>
    [HttpGet("stacks")]
    [SwaggerOperation(
        Summary = "List highlight video stacks",
        Description = "Lists highlight stacks for a class and student. " +
                      "If studentId is omitted, the authenticated user from the JWT is used."
    )]
    [ProducesResponseType(typeof(ApiResult<IReadOnlyList<HighlightVideoStackDto>>), 200)]
    [ProducesResponseType(typeof(ApiResult<object>), 401)]
    [ProducesResponseType(typeof(ApiResult<object>), 403)]
    [ProducesResponseType(typeof(ApiResult<object>), 404)]
    public async Task<IActionResult> GetStacks(
        [FromQuery] Guid classId,
        [FromQuery] Guid? studentId = null)
    {
        var result = await _personalVideoService.GetStacksAsync(classId, studentId);
        return Ok(ApiResult<IReadOnlyList<HighlightVideoStackDto>>.Success(
            result, "200", "Stacks retrieved."));
    }

    /// <summary>
    /// Returns one highlight stack with all items (max 4 videos per stack).
    /// </summary>
    [HttpGet("stacks/{stackId:guid}")]
    [SwaggerOperation(
        Summary = "Get highlight video stack",
        Description = "Retrieves one highlight stack and its video items by stack ID."
    )]
    [ProducesResponseType(typeof(ApiResult<HighlightVideoStackDto>), 200)]
    [ProducesResponseType(typeof(ApiResult<object>), 403)]
    [ProducesResponseType(typeof(ApiResult<object>), 404)]
    public async Task<IActionResult> GetStack([FromRoute] Guid stackId)
    {
        var result = await _personalVideoService.GetStackAsync(stackId);

        if (result == null)
            return NotFound(ApiResult<object>.Failure("404", "Highlight stack not found."));

        return Ok(ApiResult<HighlightVideoStackDto>.Success(result, "200", "Stack retrieved."));
    }

    /// <summary>
    /// Enqueues another Initial generation on an existing stack (same strength).
    /// </summary>
    [HttpPost("stacks/{stackId:guid}/regenerate")]
    [SwaggerOperation(
        Summary = "Regenerate highlight video on stack",
        Description = "Creates a new Initial item on the stack using the stack strength description. " +
                      "Requires an available slot (max 4) and no concurrent Processing item."
    )]
    [ProducesResponseType(typeof(ApiResult<HighlightVideoStackDto>), 202)]
    [ProducesResponseType(typeof(ApiResult<object>), 400)]
    [ProducesResponseType(typeof(ApiResult<object>), 403)]
    [ProducesResponseType(typeof(ApiResult<object>), 404)]
    [ProducesResponseType(typeof(ApiResult<object>), 409)]
    public async Task<IActionResult> RegenerateStack([FromRoute] Guid stackId)
    {
        var result = await _personalVideoService.RegenerateStackAsync(stackId);
        return Accepted(ApiResult<HighlightVideoStackDto>.Success(
            result, "202", "Highlight regeneration started."));
    }

    /// <summary>
    /// Lists ready, verified-tagged class videos for the add-segment picker.
    /// </summary>
    [HttpGet("stacks/{stackId:guid}/source-media")]
    [SwaggerOperation(
        Summary = "List source media for highlight stack",
        Description = "Returns TaggingComplete class videos verified-tagged for the stack student, " +
                      "including face segments when available. Use before POST .../add-segment."
    )]
    [ProducesResponseType(typeof(ApiResult<IReadOnlyList<HighlightSourceMediaDto>>), 200)]
    [ProducesResponseType(typeof(ApiResult<object>), 403)]
    [ProducesResponseType(typeof(ApiResult<object>), 404)]
    public async Task<IActionResult> GetSourceMedia([FromRoute] Guid stackId)
    {
        var result = await _personalVideoService.GetSourceMediaAsync(stackId);
        return Ok(ApiResult<IReadOnlyList<HighlightSourceMediaDto>>.Success(
            result, "200", "Source media retrieved."));
    }

    /// <summary>
    /// Polls progress for a highlight video item.
    /// </summary>
    [HttpGet("stacks/{stackId:guid}/items/{itemId:guid}/progress")]
    [SwaggerOperation(
        Summary = "Get highlight video item progress",
        Description = "Returns phase (BuildingClips / Encoding / terminal) and optional MediaConvert percent."
    )]
    [ProducesResponseType(typeof(ApiResult<HighlightVideoProgressDto>), 200)]
    [ProducesResponseType(typeof(ApiResult<object>), 403)]
    [ProducesResponseType(typeof(ApiResult<object>), 404)]
    public async Task<IActionResult> GetItemProgress(
        [FromRoute] Guid stackId,
        [FromRoute] Guid itemId)
    {
        var result = await _personalVideoService.GetItemProgressAsync(stackId, itemId);
        return Ok(ApiResult<HighlightVideoProgressDto>.Success(result, "200", "Progress retrieved."));
    }

    /// <summary>
    /// Cancels a Processing highlight video item.
    /// </summary>
    [HttpPost("stacks/{stackId:guid}/items/{itemId:guid}/cancel")]
    [SwaggerOperation(
        Summary = "Cancel highlight video item",
        Description = "Marks a Processing item as Cancelled and best-effort cancels the MediaConvert job. " +
                      "Late completion webhooks are ignored."
    )]
    [ProducesResponseType(typeof(ApiResult<HighlightVideoItemDto>), 200)]
    [ProducesResponseType(typeof(ApiResult<object>), 400)]
    [ProducesResponseType(typeof(ApiResult<object>), 403)]
    [ProducesResponseType(typeof(ApiResult<object>), 404)]
    public async Task<IActionResult> CancelItem(
        [FromRoute] Guid stackId,
        [FromRoute] Guid itemId)
    {
        var result = await _personalVideoService.CancelItemAsync(stackId, itemId);
        return Ok(ApiResult<HighlightVideoItemDto>.Success(result, "200", "Highlight video cancelled."));
    }

    /// <summary>
    /// Retries a Failed or Cancelled Initial highlight video item (same row).
    /// </summary>
    [HttpPost("stacks/{stackId:guid}/items/{itemId:guid}/retry")]
    [SwaggerOperation(
        Summary = "Retry failed/cancelled initial highlight item",
        Description = "Resets the same Initial item to Processing and re-enqueues generation. " +
                      "Trim/SegmentAdd failures should be deleted and re-run instead."
    )]
    [ProducesResponseType(typeof(ApiResult<HighlightVideoItemDto>), 202)]
    [ProducesResponseType(typeof(ApiResult<object>), 400)]
    [ProducesResponseType(typeof(ApiResult<object>), 403)]
    [ProducesResponseType(typeof(ApiResult<object>), 404)]
    [ProducesResponseType(typeof(ApiResult<object>), 409)]
    public async Task<IActionResult> RetryItem(
        [FromRoute] Guid stackId,
        [FromRoute] Guid itemId)
    {
        var result = await _personalVideoService.RetryItemAsync(stackId, itemId);
        return Accepted(ApiResult<HighlightVideoItemDto>.Success(
            result, "202", "Highlight video retry started."));
    }

    /// <summary>
    /// Trims a completed output video by excluding time ranges on the output timeline.
    /// </summary>
    [HttpPost("stacks/{stackId:guid}/items/{itemId:guid}/trim")]
    [SwaggerOperation(
        Summary = "Trim highlight video output",
        Description = "Starts a trim job that excludes the given time ranges from a completed highlight video."
    )]
    [ProducesResponseType(typeof(ApiResult<HighlightVideoItemDto>), 202)]
    [ProducesResponseType(typeof(ApiResult<object>), 400)]
    [ProducesResponseType(typeof(ApiResult<object>), 403)]
    [ProducesResponseType(typeof(ApiResult<object>), 404)]
    [ProducesResponseType(typeof(ApiResult<object>), 409)]
    public async Task<IActionResult> TrimItem(
        [FromRoute] Guid stackId,
        [FromRoute] Guid itemId,
        [FromBody] TrimHighlightVideoRequest request)
    {
        var result = await _personalVideoService.TrimItemAsync(stackId, itemId, request);
        return Accepted(ApiResult<HighlightVideoItemDto>.Success(result, "202", "Trim started."));
    }

    /// <summary>
    /// Inserts a user-selected source segment into the highlight manifest and re-renders.
    /// </summary>
    [HttpPost("stacks/{stackId:guid}/items/{itemId:guid}/add-segment")]
    [SwaggerOperation(
        Summary = "Add segment to highlight video",
        Description = "Inserts a source media segment into the highlight manifest by source timeline order and re-encodes."
    )]
    [ProducesResponseType(typeof(ApiResult<HighlightVideoItemDto>), 202)]
    [ProducesResponseType(typeof(ApiResult<object>), 400)]
    [ProducesResponseType(typeof(ApiResult<object>), 403)]
    [ProducesResponseType(typeof(ApiResult<object>), 404)]
    [ProducesResponseType(typeof(ApiResult<object>), 409)]
    public async Task<IActionResult> AddSegment(
        [FromRoute] Guid stackId,
        [FromRoute] Guid itemId,
        [FromBody] AddHighlightSegmentRequest request)
    {
        var result = await _personalVideoService.AddSegmentAsync(stackId, itemId, request);
        return Accepted(ApiResult<HighlightVideoItemDto>.Success(result, "202", "Segment add started."));
    }

    /// <summary>
    /// Soft-deletes one video item to free a slot in the stack (max 4 items).
    /// </summary>
    [HttpDelete("stacks/{stackId:guid}/items/{itemId:guid}")]
    [SwaggerOperation(
        Summary = "Delete highlight video item",
        Description = "Soft-deletes one highlight video item so another can be generated in the stack. " +
                      "Cannot delete while Processing; Cancelled and Failed items may be deleted."
    )]
    [ProducesResponseType(typeof(ApiResult), 200)]
    [ProducesResponseType(typeof(ApiResult<object>), 403)]
    [ProducesResponseType(typeof(ApiResult<object>), 404)]
    [ProducesResponseType(typeof(ApiResult<object>), 409)]
    public async Task<IActionResult> DeleteItem(
        [FromRoute] Guid stackId,
        [FromRoute] Guid itemId)
    {
        await _personalVideoService.DeleteItemAsync(stackId, itemId);
        return Ok(ApiResult.Success("200", "Highlight video item deleted."));
    }

    /// <summary>
    /// Soft-deletes an entire stack and its items.
    /// </summary>
    [HttpDelete("stacks/{stackId:guid}")]
    [SwaggerOperation(
        Summary = "Delete highlight video stack",
        Description = "Soft-deletes a highlight stack and all of its video items."
    )]
    [ProducesResponseType(typeof(ApiResult), 200)]
    [ProducesResponseType(typeof(ApiResult<object>), 403)]
    [ProducesResponseType(typeof(ApiResult<object>), 404)]
    [ProducesResponseType(typeof(ApiResult<object>), 409)]
    public async Task<IActionResult> DeleteStack([FromRoute] Guid stackId)
    {
        await _personalVideoService.DeleteStackAsync(stackId);
        return Ok(ApiResult.Success("200", "Highlight stack deleted."));
    }
}
