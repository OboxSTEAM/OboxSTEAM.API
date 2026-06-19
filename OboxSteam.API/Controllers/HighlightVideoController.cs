using Microsoft.AspNetCore.Mvc;
using OboxSteam.Application.DTOs.MediaDTO;
using OboxSteam.Application.Interfaces;
using OboxSteam.Application.Utils;
using Swashbuckle.AspNetCore.Annotations;

namespace OboxSteam.API.Controllers;

/// <summary>
/// Request body for triggering highlight video generation.
/// All fields are optional — omitting the body entirely uses legacy face-only behavior.
/// </summary>
public record TriggerGenerationRequest
{
    /// <summary>
    /// Optional description of the student's strengths used to filter video segments
    /// by semantic matching. (e.g. "Sinh viên có thế mạnh trong thuyết trình và đánh cờ").
    /// When null or empty, the standard face-timeline-only clipping is used.
    /// </summary>
    public string? StrengthDescription { get; init; }
}

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
    ///   (2-second buffer; segments that overlap after buffering are merged;
    ///   fallback to full video if AI cannot pinpoint the face).
    ///
    /// Strengths filtering (optional):
    /// - When <c>Strengths</c> is provided, only segments where the student is
    ///   demonstrating a matched strength are kept (via AWS Rekognition Label Detection
    ///   cross-referenced by Claude on Bedrock).
    /// - Supports Vietnamese and English strength names.
    /// - Returns 400 if no segments match any of the specified strengths.
    /// </remarks>
    [HttpPost]
    [SwaggerOperation(
        Summary = "Trigger personal video generation",
        Description = "Starts an asynchronous MediaConvert job that stitches a personalised highlight reel " +
                      "for the given student from all tagged videos in the program. Returns immediately with status=Processing. " +
                      "Supply optional 'StrengthDescription' to filter segments by student strengths (e.g. 'Sinh viên giỏi đá bóng')."
    )]
    [ProducesResponseType(typeof(ApiResult<HighlightVideoDto>), 202)]
    [ProducesResponseType(typeof(ApiResult<object>), 400)]
    [ProducesResponseType(typeof(ApiResult<object>), 404)]
    public async Task<IActionResult> TriggerGeneration(
        [FromRoute] Guid programId,
        [FromRoute] Guid studentId,
        [FromBody] TriggerGenerationRequest? request = null)
    {
        var result = await _personalVideoService.TriggerPersonalVideoGenerationAsync(
            programId, studentId, request?.StrengthDescription);
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

