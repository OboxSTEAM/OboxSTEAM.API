using Microsoft.AspNetCore.Mvc;
using OboxSteam.Application.DTOs.MediaDTO;
using OboxSteam.Application.Interfaces;
using OboxSteam.Application.Utils;
using Swashbuckle.AspNetCore.Annotations;

namespace OboxSteam.API.Controllers;

/// <summary>
/// Manages personal highlight video stacks for a student within a Program.
/// </summary>
[Route("api/programs/{programId:guid}/students/{studentId:guid}/highlight-video")]
[ApiController]
public class HighlightVideoController : ControllerBase
{
    private readonly IPersonalVideoService _personalVideoService;

    public HighlightVideoController(IPersonalVideoService personalVideoService)
    {
        _personalVideoService = personalVideoService;
    }

    /// <summary>
    /// Creates a highlight stack (max 3 per student/program) and enqueues the first video.
    /// </summary>
    [HttpPost("stacks")]
    [SwaggerOperation(Summary = "Create highlight video stack")]
    [ProducesResponseType(typeof(ApiResult<HighlightVideoStackDto>), 202)]
    public async Task<IActionResult> CreateStack(
        [FromRoute] Guid programId,
        [FromRoute] Guid studentId,
        [FromBody] CreateHighlightStackRequest? request = null)
    {
        var result = await _personalVideoService.CreateStackAsync(
            programId, studentId, request?.StrengthDescription);
        return Accepted(ApiResult<HighlightVideoStackDto>.Success(result, "202", "Highlight stack created."));
    }

    /// <summary>
    /// Lists all highlight stacks for a student in a program.
    /// </summary>
    [HttpGet("stacks")]
    [SwaggerOperation(Summary = "List highlight video stacks")]
    [ProducesResponseType(typeof(ApiResult<IReadOnlyList<HighlightVideoStackDto>>), 200)]
    public async Task<IActionResult> GetStacks(
        [FromRoute] Guid programId,
        [FromRoute] Guid studentId)
    {
        var result = await _personalVideoService.GetStacksAsync(programId, studentId);
        return Ok(ApiResult<IReadOnlyList<HighlightVideoStackDto>>.Success(result, "200", "Stacks retrieved."));
    }

    /// <summary>
    /// Returns one highlight stack with all items (max 4 videos per stack).
    /// </summary>
    [HttpGet("stacks/{stackId:guid}")]
    [SwaggerOperation(Summary = "Get highlight video stack")]
    [ProducesResponseType(typeof(ApiResult<HighlightVideoStackDto>), 200)]
    [ProducesResponseType(typeof(ApiResult<object>), 404)]
    public async Task<IActionResult> GetStack(
        [FromRoute] Guid programId,
        [FromRoute] Guid studentId,
        [FromRoute] Guid stackId)
    {
        var result = await _personalVideoService.GetStackAsync(programId, studentId, stackId);

        if (result == null)
            return NotFound(ApiResult<object>.Failure("404", "Highlight stack not found."));

        return Ok(ApiResult<HighlightVideoStackDto>.Success(result, "200", "Stack retrieved."));
    }

    /// <summary>
    /// Trims a completed output video by excluding time ranges on the output timeline.
    /// </summary>
    [HttpPost("stacks/{stackId:guid}/items/{itemId:guid}/trim")]
    [SwaggerOperation(Summary = "Trim highlight video output")]
    [ProducesResponseType(typeof(ApiResult<HighlightVideoItemDto>), 202)]
    public async Task<IActionResult> TrimItem(
        [FromRoute] Guid programId,
        [FromRoute] Guid studentId,
        [FromRoute] Guid stackId,
        [FromRoute] Guid itemId,
        [FromBody] TrimHighlightVideoRequest request)
    {
        var result = await _personalVideoService.TrimItemAsync(
            programId, studentId, stackId, itemId, request);
        return Accepted(ApiResult<HighlightVideoItemDto>.Success(result, "202", "Trim job started."));
    }

    /// <summary>
    /// Adds a segment from a source activity video into a completed highlight and re-renders.
    /// </summary>
    [HttpPost("stacks/{stackId:guid}/items/{itemId:guid}/add-segment")]
    [SwaggerOperation(Summary = "Add source segment to highlight video")]
    [ProducesResponseType(typeof(ApiResult<HighlightVideoItemDto>), 202)]
    public async Task<IActionResult> AddSegment(
        [FromRoute] Guid programId,
        [FromRoute] Guid studentId,
        [FromRoute] Guid stackId,
        [FromRoute] Guid itemId,
        [FromBody] AddHighlightSegmentRequest request)
    {
        var result = await _personalVideoService.AddSegmentAsync(
            programId, studentId, stackId, itemId, request);
        return Accepted(ApiResult<HighlightVideoItemDto>.Success(result, "202", "Segment add job started."));
    }

    /// <summary>
    /// Soft-deletes one video item to free a slot in the stack (max 4 items).
    /// </summary>
    [HttpDelete("stacks/{stackId:guid}/items/{itemId:guid}")]
    [SwaggerOperation(Summary = "Delete highlight video item")]
    [ProducesResponseType(typeof(ApiResult<object>), 200)]
    public async Task<IActionResult> DeleteItem(
        [FromRoute] Guid programId,
        [FromRoute] Guid studentId,
        [FromRoute] Guid stackId,
        [FromRoute] Guid itemId)
    {
        await _personalVideoService.DeleteItemAsync(programId, studentId, stackId, itemId);
        return Ok(ApiResult<object>.Success(null, "200", "Highlight video item deleted."));
    }

    /// <summary>
    /// Soft-deletes an entire stack and its items.
    /// </summary>
    [HttpDelete("stacks/{stackId:guid}")]
    [SwaggerOperation(Summary = "Delete highlight video stack")]
    [ProducesResponseType(typeof(ApiResult<object>), 200)]
    public async Task<IActionResult> DeleteStack(
        [FromRoute] Guid programId,
        [FromRoute] Guid studentId,
        [FromRoute] Guid stackId)
    {
        await _personalVideoService.DeleteStackAsync(programId, studentId, stackId);
        return Ok(ApiResult<object>.Success(null, "200", "Highlight stack deleted."));
    }
}
