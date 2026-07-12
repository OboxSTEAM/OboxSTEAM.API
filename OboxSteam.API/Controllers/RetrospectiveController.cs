using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OboxSteam.Application.DTOs.RetrospectiveDTO;
using OboxSteam.Application.Interfaces;
using OboxSteam.Application.Utils;
using Swashbuckle.AspNetCore.Annotations;

namespace OboxSteam.API.Controllers;

[Route("api")]
[ApiController]
public sealed class RetrospectiveController : ControllerBase
{
    private readonly IRetrospectiveAttemptService _retrospectiveAttemptService;

    public RetrospectiveController(IRetrospectiveAttemptService retrospectiveAttemptService)
    {
        _retrospectiveAttemptService = retrospectiveAttemptService;
    }

    [HttpPost("assignments/{assignmentId:guid}/retrospective/start")]
    [Authorize(Roles = "Student")]
    [SwaggerOperation(
        Summary = "Start a retrospective draft",
        Description = "Creates a new Pending submission or resumes an existing draft or revision. "
            + "Requires Student role and an active enrollment in the assignment's module.")]
    [ProducesResponseType(typeof(ApiResult<RetrospectiveAttemptResponseDto>), 201)]
    [ProducesResponseType(typeof(ApiResult<object>), 400)]
    [ProducesResponseType(typeof(ApiResult<object>), 401)]
    [ProducesResponseType(typeof(ApiResult<object>), 403)]
    [ProducesResponseType(typeof(ApiResult<object>), 404)]
    [ProducesResponseType(typeof(ApiResult<object>), 409)]
    public async Task<IActionResult> StartRetrospective(Guid assignmentId)
    {
        var result = await _retrospectiveAttemptService.StartRetrospective(assignmentId);

        return CreatedAtAction(
            nameof(GetRetrospective),
            new { submissionId = result.SubmissionId },
            ApiResult<RetrospectiveAttemptResponseDto>.Success(
                result,
                "201",
                "Retrospective draft started successfully."));
    }

    [HttpGet("submissions/{submissionId:guid}/retrospective")]
    [Authorize(Roles = "Student,Parent,Mentor,Manager,SuperAdmin")]
    [SwaggerOperation(
        Summary = "Get a retrospective submission",
        Description = "Returns plain-text content and status for a retrospective submission. "
            + "Students may only access their own submission.")]
    [ProducesResponseType(typeof(ApiResult<RetrospectiveAttemptResponseDto>), 200)]
    [ProducesResponseType(typeof(ApiResult<object>), 401)]
    [ProducesResponseType(typeof(ApiResult<object>), 403)]
    [ProducesResponseType(typeof(ApiResult<object>), 404)]
    public async Task<IActionResult> GetRetrospective(Guid submissionId)
    {
        var result = await _retrospectiveAttemptService.GetRetrospective(submissionId);
        if (result == null)
        {
            return NotFound(ApiResult<object>.Failure("404", "Retrospective submission not found."));
        }

        return Ok(ApiResult<RetrospectiveAttemptResponseDto>.Success(
            result,
            "200",
            "Retrospective submission retrieved successfully."));
    }

    [HttpPut("submissions/{submissionId:guid}/retrospective/draft")]
    [Authorize(Roles = "Student")]
    [SwaggerOperation(
        Summary = "Save retrospective draft",
        Description = "Autosaves plain-text draft content for a Pending or ReturnedForRevision submission.")]
    [ProducesResponseType(typeof(ApiResult<SaveRetrospectiveDraftResponseDto>), 200)]
    [ProducesResponseType(typeof(ApiResult<object>), 400)]
    [ProducesResponseType(typeof(ApiResult<object>), 401)]
    [ProducesResponseType(typeof(ApiResult<object>), 403)]
    [ProducesResponseType(typeof(ApiResult<object>), 404)]
    [ProducesResponseType(typeof(ApiResult<object>), 409)]
    public async Task<IActionResult> SaveDraft(
        Guid submissionId,
        [FromBody, SwaggerParameter("Plain-text draft")] SaveRetrospectiveDraftRequestDto request)
    {
        var result = await _retrospectiveAttemptService.SaveDraft(submissionId, request);

        return Ok(ApiResult<SaveRetrospectiveDraftResponseDto>.Success(
            result,
            "200",
            "Retrospective draft saved successfully."));
    }

    [HttpPost("submissions/{submissionId:guid}/retrospective/submit")]
    [Authorize(Roles = "Student")]
    [SwaggerOperation(
        Summary = "Submit retrospective",
        Description = "Final submit: merges optional request text with the saved draft, "
            + "requires non-empty plain text, and sets submission to TurnedIn for mentor grading.")]
    [ProducesResponseType(typeof(ApiResult<RetrospectiveAttemptResponseDto>), 200)]
    [ProducesResponseType(typeof(ApiResult<object>), 400)]
    [ProducesResponseType(typeof(ApiResult<object>), 401)]
    [ProducesResponseType(typeof(ApiResult<object>), 403)]
    [ProducesResponseType(typeof(ApiResult<object>), 404)]
    [ProducesResponseType(typeof(ApiResult<object>), 409)]
    public async Task<IActionResult> SubmitRetrospective(
        Guid submissionId,
        [FromBody, SwaggerParameter("Optional final text")] SubmitRetrospectiveRequestDto? request)
    {
        var result = await _retrospectiveAttemptService.SubmitRetrospective(
            submissionId,
            request ?? new SubmitRetrospectiveRequestDto());

        return Ok(ApiResult<RetrospectiveAttemptResponseDto>.Success(
            result,
            "200",
            "Retrospective submitted successfully."));
    }
}
