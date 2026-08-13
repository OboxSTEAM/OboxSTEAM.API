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

    [HttpGet("research-submissions/{submissionId:guid}")]
    [Authorize(Roles = "Student,Parent,Mentor,Manager,Admin")]
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

    [HttpPost("research-submissions/upload")]
    [Authorize(Roles = "Student")]
    [RequestSizeLimit(3L * 1024 * 1024 * 1024)]
    [RequestFormLimits(MultipartBodyLengthLimit = 3L * 1024 * 1024 * 1024)]
    [SwaggerOperation(
        Summary = "Upload research submission file to S3",
        Description = "Uploads a deliverable or evidence file to S3. Lazy-creates a Pending draft when the "
            + "milestone is unlocked. Returns FileUrl or EvidenceUrls (and SubmissionId) for use in submit. "
            + "Set isEvidence=true for supporting evidence files.")]
    [ProducesResponseType(typeof(ApiResult<UploadResearchSubmissionResponseDto>), 200)]
    [ProducesResponseType(typeof(ApiResult<object>), 400)]
    [ProducesResponseType(typeof(ApiResult<object>), 401)]
    [ProducesResponseType(typeof(ApiResult<object>), 403)]
    [ProducesResponseType(typeof(ApiResult<object>), 404)]
    [ProducesResponseType(typeof(ApiResult<object>), 409)]
    public async Task<IActionResult> UploadSubmissionFile(
        [FromQuery, SwaggerParameter("Module enrollment id")] Guid moduleEnrollmentId,
        [FromQuery, SwaggerParameter("Research milestone id")] Guid researchMilestoneId,
        IFormFile file,
        [FromQuery, SwaggerParameter("When true, URL is returned in EvidenceUrls instead of FileUrl")]
        bool isEvidence = false)
    {
        var result = await _researchSubmissionService.UploadSubmissionFile(
            moduleEnrollmentId,
            researchMilestoneId,
            file,
            isEvidence);

        return Ok(ApiResult<UploadResearchSubmissionResponseDto>.Success(
            result,
            "200",
            "Research submission file uploaded successfully."));
    }

    [HttpPost("research-submissions/submit")]
    [Authorize(Roles = "Student")]
    [SwaggerOperation(
        Summary = "Submit research work",
        Description = "Student submits research deliverable content for a milestone. Creates the submission "
            + "when none exists (milestone unlock + required activities + availability). "
            + "Resubmission after ReturnedForRevision does not require mentor to reopen. "
            + "Personal AvailableUntil from approved assessment recovery is honored.")]
    [ProducesResponseType(typeof(ApiResult<ResearchSubmissionResponseDto>), 200)]
    [ProducesResponseType(typeof(ApiResult<object>), 400)]
    [ProducesResponseType(typeof(ApiResult<object>), 401)]
    [ProducesResponseType(typeof(ApiResult<object>), 403)]
    [ProducesResponseType(typeof(ApiResult<object>), 404)]
    [ProducesResponseType(typeof(ApiResult<object>), 409)]
    public async Task<IActionResult> SubmitResearchWork(
        [FromBody, SwaggerParameter("Research work content")] SubmitResearchWorkRequestDto request)
    {
        var result = await _researchSubmissionService.SubmitResearchWork(request);

        return Ok(ApiResult<ResearchSubmissionResponseDto>.Success(
            result,
            "200",
            "Research work submitted successfully."));
    }

    [HttpPost("research-submissions/{submissionId:guid}/grade")]
    [Authorize(Roles = "Mentor,Manager,Admin")]
    [SwaggerOperation(
        Summary = "Grade a research submission",
        Description = "Mentor, Manager, or Admin grades a turned-in submission or returns it for revision.")]
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
