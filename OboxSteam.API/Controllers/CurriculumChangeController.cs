using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OboxSteam.Application.DTOs.CurriculumChangeDTO;
using OboxSteam.Application.Interfaces;
using OboxSteam.Application.Utils;
using Swashbuckle.AspNetCore.Annotations;

namespace OboxSteam.API.Controllers;

[Route("api/programs/{id:guid}/curriculum/changes")]
[ApiController]
public sealed class CurriculumChangeController : ControllerBase
{
    private readonly ICurriculumChangeService _curriculumChangeService;

    public CurriculumChangeController(ICurriculumChangeService curriculumChangeService)
    {
        _curriculumChangeService = curriculumChangeService;
    }

    [HttpGet]
    [Authorize(Roles = "Expert,Manager,Admin")]
    [SwaggerOperation(
        Summary = "Net curriculum changes between two versions",
        Description = "Advisory participants only. base = lastApproval (default; 0 when never approved), lastSeen, start, or version:N. " +
                      "to defaults to the current curriculumVersion. Items are consolidated per component and sorted in current tree order.")]
    [ProducesResponseType(typeof(ApiResult<CurriculumChangesDto>), 200)]
    public async Task<IActionResult> GetChanges(
        [FromRoute] Guid id,
        [FromQuery(Name = "base")] string? baseSpec,
        [FromQuery] long? to)
    {
        var result = await _curriculumChangeService.GetChangesAsync(id, baseSpec, to);
        return Ok(ApiResult<CurriculumChangesDto>.Success(result, "200", "Curriculum changes retrieved successfully."));
    }

    [HttpPost("seen")]
    [Authorize(Roles = "Expert,Manager,Admin")]
    [SwaggerOperation(
        Summary = "Mark curriculum changes as seen",
        Description = "Stores max(seenVersion, version) for the caller. version above the current curriculumVersion returns 400.")]
    [ProducesResponseType(typeof(ApiResult<object>), 200)]
    public async Task<IActionResult> MarkSeen(
        [FromRoute] Guid id,
        [FromBody] MarkCurriculumChangesSeenRequest request)
    {
        await _curriculumChangeService.MarkSeenAsync(id, request);
        return Ok(ApiResult<object>.Success(new { }, "200", "Curriculum changes marked as seen."));
    }
}
