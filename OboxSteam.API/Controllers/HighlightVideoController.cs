using Microsoft.AspNetCore.Mvc;
using OboxSteam.Application.DTOs.MediaDTO;
using OboxSteam.Application.Interfaces;
using OboxSteam.Application.Utils;
using Swashbuckle.AspNetCore.Annotations;

namespace OboxSteam.API.Controllers;

/// <summary>
/// Manages personal highlight video generation for a student within a Program.
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
    /// Trigger personal video generation for a student in a program.
    /// </summary>
    /// <remarks>
    /// Collects all tagged, processed videos belonging to the program, applies the
    /// clipping Logic Core rules, and submits a MediaConvert stitching job.
    /// The endpoint returns immediately with a <c>Processing</c> status.
    /// Poll GET to check progress.
    ///
    /// Clipping rules:
    /// - Scene-only video (no faces detected) → included in full.
    /// - Video where only the student's face appears → included in full.
    /// - Video with multiple people → only the student's segments are extracted
    ///   (3-second buffer + merge if gap &lt; 3 s; fallback to full video if AI
    ///   cannot pinpoint the face).
    /// </remarks>
    [HttpPost]
    [SwaggerOperation(
        Summary = "Trigger personal video generation",
        Description = "Starts an asynchronous MediaConvert job that stitches a personalised highlight reel " +
                      "for the given student from all tagged videos in the program. Returns immediately with status=Processing."
    )]
    [ProducesResponseType(typeof(ApiResult<HighlightVideoDto>), 202)]
    [ProducesResponseType(typeof(ApiResult<object>), 400)]
    [ProducesResponseType(typeof(ApiResult<object>), 404)]
    public async Task<IActionResult> TriggerGeneration(
        [FromRoute] Guid programId,
        [FromRoute] Guid studentId)
    {
        var result = await _personalVideoService.TriggerPersonalVideoGenerationAsync(programId, studentId);
        return Accepted(ApiResult<HighlightVideoDto>.Success(result, "202", "Personal video generation started."));
    }

    /// <summary>
    /// Get the current status and URL of a student's personal highlight video.
    /// </summary>
    [HttpGet]
    [SwaggerOperation(
        Summary = "Get personal highlight video status",
        Description = "Returns the current generation status (None/Processing/Completed/Failed) and, " +
                      "when complete, the public video URL."
    )]
    [ProducesResponseType(typeof(ApiResult<HighlightVideoDto>), 200)]
    [ProducesResponseType(typeof(ApiResult<object>), 404)]
    public async Task<IActionResult> GetHighlightVideo(
        [FromRoute] Guid programId,
        [FromRoute] Guid studentId)
    {
        var result = await _personalVideoService.GetHighlightVideoAsync(programId, studentId);

        if (result == null)
            return NotFound(ApiResult<object>.Failure("404", "No highlight video found for this student and program."));

        return Ok(ApiResult<HighlightVideoDto>.Success(result, "200", "Highlight video retrieved."));
    }
}
