using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OboxSteam.Application.DTOs.ClassRedeliveryDTO;
using OboxSteam.Application.Interfaces;
using OboxSteam.Application.Utils;
using Swashbuckle.AspNetCore.Annotations;

namespace OboxSteam.API.Controllers;

[Route("api/manager/redelivery")]
[ApiController]
public class ManagerRedeliveryController : ControllerBase
{
    private readonly IClassRedeliveryRequestService _service;

    public ManagerRedeliveryController(IClassRedeliveryRequestService service)
    {
        _service = service;
    }

    [HttpGet("waitlist")]
    [Authorize(Roles = "Manager,Admin")]
    [SwaggerOperation(Summary = "Re-delivery waitlist grouped by program then module")]
    [ProducesResponseType(typeof(ApiResult<List<RedeliveryWaitlistProgramGroupDto>>), 200)]
    public async Task<IActionResult> GetWaitlist()
    {
        var result = await _service.GetWaitlistGroupedAsync();
        return Ok(ApiResult<List<RedeliveryWaitlistProgramGroupDto>>.Success(
            result, "200", "Re-delivery waitlist retrieved."));
    }

    [HttpPost("open-remedial-class")]
    [Authorize(Roles = "Manager,Admin")]
    [SwaggerOperation(Summary = "Open an intensive remedial class and offer it to the module waitlist")]
    [ProducesResponseType(typeof(ApiResult<OpenRemedialClassResponseDto>), 201)]
    public async Task<IActionResult> OpenRemedialClass([FromBody] OpenRemedialClassRequestDto dto)
    {
        var result = await _service.OpenRemedialClassAsync(dto);
        return StatusCode(201, ApiResult<OpenRemedialClassResponseDto>.Success(
            result, "201", "Remedial class opened."));
    }
}
