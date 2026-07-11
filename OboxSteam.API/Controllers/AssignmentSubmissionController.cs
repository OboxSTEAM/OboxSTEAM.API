using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OboxSteam.Application.DTOs.AssignmentSubmissionDTO;
using OboxSteam.Application.Interfaces;
using OboxSteam.Application.Utils;
using Swashbuckle.AspNetCore.Annotations;

namespace OboxSteam.API.Controllers;

[Route("api")]
[ApiController]
public sealed class AssignmentSubmissionController : ControllerBase
{
    private readonly IAssignmentSubmissionService _assignmentSubmissionService;

    public AssignmentSubmissionController(IAssignmentSubmissionService assignmentSubmissionService)
    {
        _assignmentSubmissionService = assignmentSubmissionService;
    }

    [HttpPost("assignment-submissions/submit")]
    [Authorize(Roles = "Student")]
    [SwaggerOperation(
        Summary = "Submit work for a non-research assignment",
        Description = "Student turns in a FileUpload, Retrospective, or Practical assignment. "
            + "Quizzes and research milestones use their dedicated endpoints. "
            + "Provide ContentText and/or FileUrl.")]
    [ProducesResponseType(typeof(ApiResult<AssignmentSubmissionResponseDto>), 200)]
    [ProducesResponseType(typeof(ApiResult<object>), 400)]
    [ProducesResponseType(typeof(ApiResult<object>), 401)]
    [ProducesResponseType(typeof(ApiResult<object>), 403)]
    [ProducesResponseType(typeof(ApiResult<object>), 404)]
    [ProducesResponseType(typeof(ApiResult<object>), 409)]
    public async Task<IActionResult> SubmitAssignment(
        [FromBody, SwaggerParameter("Assignment submission content")] SubmitAssignmentRequestDto request)
    {
        var result = await _assignmentSubmissionService.SubmitAssignment(request);

        return Ok(ApiResult<AssignmentSubmissionResponseDto>.Success(
            result,
            "200",
            "Assignment submitted successfully."));
    }

    [HttpGet("assignment-submissions/{submissionId:guid}")]
    [Authorize(Roles = "Student,Parent,Mentor,Manager,SuperAdmin")]
    [SwaggerOperation(
        Summary = "Get a non-research assignment submission by ID",
        Description = "Returns a FileUpload, Retrospective, or Practical submission. Access is enforced per role.")]
    [ProducesResponseType(typeof(ApiResult<AssignmentSubmissionResponseDto>), 200)]
    [ProducesResponseType(typeof(ApiResult<object>), 400)]
    [ProducesResponseType(typeof(ApiResult<object>), 401)]
    [ProducesResponseType(typeof(ApiResult<object>), 403)]
    [ProducesResponseType(typeof(ApiResult<object>), 404)]
    public async Task<IActionResult> GetAssignmentSubmission([FromRoute] Guid submissionId)
    {
        var result = await _assignmentSubmissionService.GetAssignmentSubmission(submissionId);
        if (result == null)
        {
            return NotFound(ApiResult<object>.Failure("404", "Assignment submission not found."));
        }

        return Ok(ApiResult<AssignmentSubmissionResponseDto>.Success(
            result,
            "200",
            "Assignment submission retrieved successfully."));
    }

    [HttpPost("assignment-submissions/{submissionId:guid}/upload")]
    [Authorize(Roles = "Student")]
    [RequestSizeLimit(3L * 1024 * 1024 * 1024)]
    [RequestFormLimits(MultipartBodyLengthLimit = 3L * 1024 * 1024 * 1024)]
    [SwaggerOperation(
        Summary = "Upload an assignment submission file to S3",
        Description = "Uploads a deliverable file to S3 only (no DB write) and returns the file URL for use in submit.")]
    [ProducesResponseType(typeof(ApiResult<string>), 200)]
    [ProducesResponseType(typeof(ApiResult<object>), 400)]
    [ProducesResponseType(typeof(ApiResult<object>), 401)]
    [ProducesResponseType(typeof(ApiResult<object>), 403)]
    [ProducesResponseType(typeof(ApiResult<object>), 404)]
    public async Task<IActionResult> UploadAssignmentFile(
        [FromRoute] Guid submissionId,
        IFormFile file)
    {
        var result = await _assignmentSubmissionService.UploadAssignmentFile(submissionId, file);

        return Ok(ApiResult<string>.Success(
            result,
            "200",
            "Assignment submission file uploaded successfully."));
    }

    [HttpPost("assignment-submissions/{submissionId:guid}/grade")]
    [Authorize(Roles = "Mentor,Manager,SuperAdmin")]
    [SwaggerOperation(
        Summary = "Grade a non-research assignment submission",
        Description = "Mentor, Manager, or SuperAdmin grades a turned-in submission against the assignment's pass score, "
            + "or returns it for revision. Grading recalculates module and program progress.")]
    [ProducesResponseType(typeof(ApiResult<AssignmentSubmissionResponseDto>), 200)]
    [ProducesResponseType(typeof(ApiResult<object>), 400)]
    [ProducesResponseType(typeof(ApiResult<object>), 401)]
    [ProducesResponseType(typeof(ApiResult<object>), 403)]
    [ProducesResponseType(typeof(ApiResult<object>), 404)]
    [ProducesResponseType(typeof(ApiResult<object>), 409)]
    public async Task<IActionResult> GradeAssignment(
        [FromRoute] Guid submissionId,
        [FromBody, SwaggerParameter("Grade request")] GradeAssignmentSubmissionRequestDto request)
    {
        var result = await _assignmentSubmissionService.GradeAssignment(submissionId, request);

        return Ok(ApiResult<AssignmentSubmissionResponseDto>.Success(
            result,
            "200",
            "Assignment submission graded successfully."));
    }
}
