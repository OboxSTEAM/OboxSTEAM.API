using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OboxSteam.Application.DTOs.ResearchSubmissionDTO;
using OboxSteam.Application.Interfaces;
using OboxSteam.Application.Utils;
using Swashbuckle.AspNetCore.Annotations;

namespace OboxSteam.API.Controllers;

[Route("api")]
[ApiController]
public sealed class ResearchSubmissionController : ControllerBase
{
    private readonly IResearchSubmissionService _researchSubmissionService;

    public ResearchSubmissionController(IResearchSubmissionService researchSubmissionService)
    {
        _researchSubmissionService = researchSubmissionService;
    }

    [HttpPost("research-submissions/start")]
    [Authorize(Roles = "Mentor,Manager,SuperAdmin")]
    [SwaggerOperation(
        Summary = "Open a research submission for a student",
        Description = "Mentor, Manager, or SuperAdmin opens a submission slot for a student on a research milestone. "
            + "The student can then submit work within the assignment availability window.")]
    [ProducesResponseType(typeof(ApiResult<ResearchSubmissionResponseDto>), 201)]
    [ProducesResponseType(typeof(ApiResult<object>), 400)]
    [ProducesResponseType(typeof(ApiResult<object>), 401)]
    [ProducesResponseType(typeof(ApiResult<object>), 403)]
    [ProducesResponseType(typeof(ApiResult<object>), 404)]
    [ProducesResponseType(typeof(ApiResult<object>), 409)]
    public async Task<IActionResult> StartSubmission(
        [FromBody, SwaggerParameter("Start submission request")] StartResearchSubmissionRequestDto request)
    {
        var result = await _researchSubmissionService.StartSubmission(request);

        return CreatedAtAction(
            nameof(GetSubmission),
            new { submissionId = result.Id },
            ApiResult<ResearchSubmissionResponseDto>.Success(
                result,
                "201",
                "Research submission opened successfully."));
    }

    [HttpGet("research-submissions/{submissionId:guid}")]
    [Authorize(Roles = "Student,Parent,Mentor,Manager,SuperAdmin")]
    [SwaggerOperation(
        Summary = "Get research submission by ID",
        Description = "Returns a research milestone submission. Access is enforced per role.")]
    [ProducesResponseType(typeof(ApiResult<ResearchSubmissionResponseDto>), 200)]
    [ProducesResponseType(typeof(ApiResult<object>), 401)]
    [ProducesResponseType(typeof(ApiResult<object>), 403)]
    [ProducesResponseType(typeof(ApiResult<object>), 404)]
    public async Task<IActionResult> GetSubmission([FromRoute] Guid submissionId)
    {
        var result = await _researchSubmissionService.GetSubmission(submissionId);
        if (result == null)
        {
            return NotFound(ApiResult<object>.Failure("404", "Research submission not found."));
        }

        return Ok(ApiResult<ResearchSubmissionResponseDto>.Success(
            result,
            "200",
            "Research submission retrieved successfully."));
    }

    [HttpPost("research-submissions/{submissionId:guid}/upload")]
    [Authorize(Roles = "Student")]
    [RequestSizeLimit(3L * 1024 * 1024 * 1024)]
    [RequestFormLimits(MultipartBodyLengthLimit = 3L * 1024 * 1024 * 1024)]
    [SwaggerOperation(
        Summary = "Upload research submission file to S3",
        Description = "Uploads a deliverable or evidence file to S3 only (no DB write). "
            + "Returns CreateResearchSubmissionRequestDto with FileUrl or EvidenceUrls for use in submit. "
            + "Set isEvidence=true for supporting evidence files.")]
    [ProducesResponseType(typeof(ApiResult<CreateResearchSubmissionRequestDto>), 200)]
    [ProducesResponseType(typeof(ApiResult<object>), 400)]
    [ProducesResponseType(typeof(ApiResult<object>), 401)]
    [ProducesResponseType(typeof(ApiResult<object>), 403)]
    [ProducesResponseType(typeof(ApiResult<object>), 404)]
    [ProducesResponseType(typeof(ApiResult<object>), 409)]
    public async Task<IActionResult> UploadSubmissionFile(
        [FromRoute] Guid submissionId,
        IFormFile file,
        [FromQuery, SwaggerParameter("When true, URL is returned in EvidenceUrls instead of FileUrl")]
        bool isEvidence = false)
    {
        var result = await _researchSubmissionService.UploadSubmissionFile(submissionId, file, isEvidence);

        return Ok(ApiResult<CreateResearchSubmissionRequestDto>.Success(
            result,
            "200",
            "Research submission file uploaded successfully."));
    }

    [HttpPost("research-submissions/{submissionId:guid}/submit")]
    [Authorize(Roles = "Student")]
    [SwaggerOperation(
        Summary = "Submit research work",
        Description = "Student submits research deliverable content in a single action. "
            + "No draft saving. Resubmission after ReturnedForRevision does not require mentor to reopen.")]
    [ProducesResponseType(typeof(ApiResult<ResearchSubmissionResponseDto>), 200)]
    [ProducesResponseType(typeof(ApiResult<object>), 400)]
    [ProducesResponseType(typeof(ApiResult<object>), 401)]
    [ProducesResponseType(typeof(ApiResult<object>), 403)]
    [ProducesResponseType(typeof(ApiResult<object>), 404)]
    [ProducesResponseType(typeof(ApiResult<object>), 409)]
    public async Task<IActionResult> SubmitResearchWork(
        [FromRoute] Guid submissionId,
        [FromBody, SwaggerParameter("Research work content")] CreateResearchSubmissionRequestDto request)
    {
        var result = await _researchSubmissionService.SubmitResearchWork(submissionId, request);

        return Ok(ApiResult<ResearchSubmissionResponseDto>.Success(
            result,
            "200",
            "Research work submitted successfully."));
    }

    [HttpPost("research-submissions/{submissionId:guid}/grade")]
    [Authorize(Roles = "Mentor,Manager,SuperAdmin")]
    [SwaggerOperation(
        Summary = "Grade a research submission",
        Description = "Mentor, Manager, or SuperAdmin grades a turned-in submission or returns it for revision.")]
    [ProducesResponseType(typeof(ApiResult<ResearchSubmissionResponseDto>), 200)]
    [ProducesResponseType(typeof(ApiResult<object>), 400)]
    [ProducesResponseType(typeof(ApiResult<object>), 401)]
    [ProducesResponseType(typeof(ApiResult<object>), 403)]
    [ProducesResponseType(typeof(ApiResult<object>), 404)]
    [ProducesResponseType(typeof(ApiResult<object>), 409)]
    public async Task<IActionResult> GradeSubmission(
        [FromRoute] Guid submissionId,
        [FromBody, SwaggerParameter("Grade request")] GradeResearchSubmissionRequestDto request)
    {
        var result = await _researchSubmissionService.GradeSubmission(submissionId, request);

        return Ok(ApiResult<ResearchSubmissionResponseDto>.Success(
            result,
            "200",
            "Research submission graded successfully."));
    }
}
