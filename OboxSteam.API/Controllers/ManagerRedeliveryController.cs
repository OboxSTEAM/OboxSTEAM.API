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
    private const string GoneMessage =
        "Manager waitlist / remedial intensive redelivery is no longer available. "
        + "Students pick a Standard class via the continuity catalog.";

    private readonly IClassRedeliveryRequestService _service;

    public ManagerRedeliveryController(IClassRedeliveryRequestService service)
    {
        _service = service;
    }

    [HttpGet("waitlist")]
    [Authorize(Roles = "Manager,Admin")]
    [SwaggerOperation(Summary = "Removed — redelivery waitlist is no longer available")]
    [ProducesResponseType(typeof(ApiResult<object>), 410)]
    public async Task<IActionResult> GetWaitlist()
    {
        await _service.GetWaitlistGroupedAsync();
        return StatusCode(410, ApiResult<object>.Failure("410", GoneMessage));
    }

    [HttpPost("open-remedial-class")]
    [Authorize(Roles = "Manager,Admin")]
    [SwaggerOperation(Summary = "Removed — open remedial class is no longer available")]
    [ProducesResponseType(typeof(ApiResult<object>), 410)]
    public async Task<IActionResult> OpenRemedialClass([FromBody] OpenRemedialClassRequestDto dto)
    {
        await _service.OpenRemedialClassAsync(dto);
        return StatusCode(410, ApiResult<object>.Failure("410", GoneMessage));
    }
}
