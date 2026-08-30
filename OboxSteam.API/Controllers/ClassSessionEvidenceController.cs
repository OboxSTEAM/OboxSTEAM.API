using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OboxSteam.Application.DTOs.MediaDTO;
using OboxSteam.Application.Interfaces;
using OboxSteam.Application.Utils;
using Swashbuckle.AspNetCore.Annotations;

namespace OboxSteam.API.Controllers;

/// <summary>Mentor Offline session evidence photos (no face recognition).</summary>
[Route("api/class-sessions")]
[ApiController]
[Authorize]
public class ClassSessionEvidenceController : ControllerBase
{
    private readonly ISessionEvidenceService _sessionEvidenceService;

    public ClassSessionEvidenceController(ISessionEvidenceService sessionEvidenceService)
    {
        _sessionEvidenceService = sessionEvidenceService;
    }

    [HttpPost("{id:guid}/evidence")]
    [Authorize(Roles = "Mentor,Manager,Admin")]
    [RequestSizeLimit(10L * 1024 * 1024)]
    [RequestFormLimits(MultipartBodyLengthLimit = 10L * 1024 * 1024)]
    [SwaggerOperation(
        Summary = "Upload session evidence photo",
        Description = "Uploads an image (.jpg/.jpeg/.png) as Offline field evidence for the session. "
            + "Does not require student face recognition. Stored under session-evidence/{sessionId}/.")]
    [ProducesResponseType(typeof(ApiResult<MediaAssetDto>), 201)]
    [ProducesResponseType(typeof(ApiResult<object>), 400)]
    [ProducesResponseType(typeof(ApiResult<object>), 403)]
    [ProducesResponseType(typeof(ApiResult<object>), 404)]
    public async Task<IActionResult> UploadEvidence(
        [FromRoute] Guid id,
        IFormFile file)
    {
        if (file == null)
            return BadRequest(ApiResult<object>.Failure("400", "Evidence image file is required."));

        var result = await _sessionEvidenceService.UploadEvidenceAsync(id, file);
        return StatusCode(
            StatusCodes.Status201Created,
            ApiResult<MediaAssetDto>.Success(result, "201", "Evidence uploaded."));
    }

    [HttpGet("{id:guid}/evidence")]
    [Authorize(Roles = "Student,Mentor,Manager,Admin")]
    [SwaggerOperation(
        Summary = "List session evidence photos",
        Description = "Returns image evidence attached to the class session.")]
    [ProducesResponseType(typeof(ApiResult<IReadOnlyList<MediaAssetDto>>), 200)]
    [ProducesResponseType(typeof(ApiResult<object>), 403)]
    [ProducesResponseType(typeof(ApiResult<object>), 404)]
    public async Task<IActionResult> ListEvidence([FromRoute] Guid id)
    {
        var result = await _sessionEvidenceService.ListEvidenceAsync(id);
        return Ok(ApiResult<IReadOnlyList<MediaAssetDto>>.Success(
            result,
            "200",
            "Evidence retrieved."));
    }

    [HttpDelete("{id:guid}/evidence/{mediaId:guid}")]
    [Authorize(Roles = "Mentor,Manager,Admin")]
    [SwaggerOperation(
        Summary = "Delete session evidence photo",
        Description = "Soft-deletes the evidence media and removes the S3 object.")]
    [ProducesResponseType(typeof(ApiResult), 200)]
    [ProducesResponseType(typeof(ApiResult<object>), 400)]
    [ProducesResponseType(typeof(ApiResult<object>), 403)]
    [ProducesResponseType(typeof(ApiResult<object>), 404)]
    public async Task<IActionResult> DeleteEvidence(
        [FromRoute] Guid id,
        [FromRoute] Guid mediaId)
    {
        await _sessionEvidenceService.DeleteEvidenceAsync(id, mediaId);
        return Ok(ApiResult.Success("200", "Evidence deleted."));
    }
}
