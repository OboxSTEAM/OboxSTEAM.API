using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OboxSteam.Application.DTOs.ClassDTO;
using OboxSteam.Application.DTOs.ClassRedeliveryDTO;
using OboxSteam.Application.Interfaces;
using OboxSteam.Application.Utils;
using Swashbuckle.AspNetCore.Annotations;

namespace OboxSteam.API.Controllers;

[Route("api/class-redelivery-requests")]
[ApiController]
public class ClassRedeliveryRequestController : ControllerBase
{
    private const string GoneMessage =
        "Manager waitlist / remedial intensive redelivery is no longer available. "
        + "Students pick a Standard class via the continuity catalog.";

    private readonly IClassRedeliveryRequestService _service;

    public ClassRedeliveryRequestController(IClassRedeliveryRequestService service)
    {
        _service = service;
    }

    [HttpPost]
    [Authorize(Roles = "Student,Mentor,Manager,Admin")]
    [SwaggerOperation(Summary = "Request class continuity (always AwaitingClassSelection)")]
    [ProducesResponseType(typeof(ApiResult<ClassRedeliveryRequestResponseDto>), 201)]
    public async Task<IActionResult> Create([FromBody] CreateClassRedeliveryRequestDto dto)
    {
        var result = await _service.CreateAsync(dto);
        return StatusCode(201, ApiResult<ClassRedeliveryRequestResponseDto>.Success(
            result, "201", "Class re-delivery request created."));
    }

    [HttpGet("me")]
    [Authorize(Roles = "Student,Mentor,Manager,Admin")]
    [SwaggerOperation(Summary = "List my class re-delivery requests")]
    [ProducesResponseType(typeof(ApiResult<List<ClassRedeliveryRequestResponseDto>>), 200)]
    public async Task<IActionResult> GetMine()
    {
        var result = await _service.GetMineAsync();
        return Ok(ApiResult<List<ClassRedeliveryRequestResponseDto>>.Success(
            result, "200", "Re-delivery requests retrieved."));
    }

    [HttpGet("{id:guid}/candidates")]
    [Authorize(Roles = "Student,Mentor,Manager,Admin")]
    [SwaggerOperation(
        Summary = "Continuity class catalog for an open redelivery request",
        Description = "Same shape as GET /api/programs/{id}/rebuy-classes (RebuyClassCatalogDto).")]
    [ProducesResponseType(typeof(ApiResult<RebuyClassCatalogDto>), 200)]
    public async Task<IActionResult> GetCandidates([FromRoute] Guid id)
    {
        var result = await _service.GetCandidatesAsync(id);
        return Ok(ApiResult<RebuyClassCatalogDto>.Success(
            result, "200", "Continuity class catalog retrieved."));
    }

    [HttpPost("{id:guid}/select-class")]
    [Authorize(Roles = "Student")]
    [SwaggerOperation(Summary = "Student picks a class for continuity (then pays RetakeFee ?? Price)")]
    [ProducesResponseType(typeof(ApiResult<ClassRedeliveryRequestResponseDto>), 200)]
    public async Task<IActionResult> SelectClass(
        [FromRoute] Guid id,
        [FromBody] SelectClassRedeliveryRequestDto dto)
    {
        var result = await _service.SelectClassAsync(id, dto.ClassId);
        return Ok(ApiResult<ClassRedeliveryRequestResponseDto>.Success(
            result, "200", "Class selected; payment pending."));
    }

    [HttpPost("{id:guid}/accept-intensive")]
    [Authorize(Roles = "Student")]
    [SwaggerOperation(Summary = "Removed — intensive remedial consent is no longer available")]
    [ProducesResponseType(typeof(ApiResult<object>), 410)]
    public async Task<IActionResult> AcceptIntensive([FromRoute] Guid id)
    {
        await _service.AcceptIntensiveAsync(id);
        return StatusCode(410, ApiResult<object>.Failure("410", GoneMessage));
    }

    [HttpPost("{id:guid}/decline-intensive")]
    [Authorize(Roles = "Student")]
    [SwaggerOperation(Summary = "Removed — intensive remedial consent is no longer available")]
    [ProducesResponseType(typeof(ApiResult<object>), 410)]
    public async Task<IActionResult> DeclineIntensive([FromRoute] Guid id)
    {
        await _service.DeclineIntensiveAsync(id);
        return StatusCode(410, ApiResult<object>.Failure("410", GoneMessage));
    }

    [HttpGet("pending-manager")]
    [Authorize(Roles = "Manager,Admin")]
    [SwaggerOperation(Summary = "Removed — manager redelivery queue is no longer available")]
    [ProducesResponseType(typeof(ApiResult<object>), 410)]
    public async Task<IActionResult> GetPendingManager()
    {
        await _service.GetPendingManagerAsync();
        return StatusCode(410, ApiResult<object>.Failure("410", GoneMessage));
    }

    [HttpPost("{id:guid}/withdraw")]
    [Authorize(Roles = "Student,Mentor,Manager,Admin")]
    [SwaggerOperation(Summary = "Cancel an open re-delivery request (does not close the program enrollment)")]
    [ProducesResponseType(typeof(ApiResult<ClassRedeliveryRequestResponseDto>), 200)]
    public async Task<IActionResult> Withdraw([FromRoute] Guid id)
    {
        var result = await _service.WithdrawAsync(id);
        return Ok(ApiResult<ClassRedeliveryRequestResponseDto>.Success(
            result, "200", "Re-delivery request withdrawn."));
    }

    [HttpPost("{id:guid}/assign-target")]
    [Authorize(Roles = "Manager,Admin")]
    [SwaggerOperation(Summary = "Removed — manager assign-target is no longer available")]
    [ProducesResponseType(typeof(ApiResult<object>), 410)]
    public async Task<IActionResult> AssignTarget(
        [FromRoute] Guid id,
        [FromBody] DecideClassRedeliveryRequestDto dto)
    {
        await _service.ManagerAssignTargetAsync(id, dto);
        return StatusCode(410, ApiResult<object>.Failure("410", GoneMessage));
    }

    [HttpPost("{id:guid}/reject")]
    [Authorize(Roles = "Manager,Admin")]
    [SwaggerOperation(Summary = "Removed — manager reject is no longer available")]
    [ProducesResponseType(typeof(ApiResult<object>), 410)]
    public async Task<IActionResult> Reject(
        [FromRoute] Guid id,
        [FromBody] DecideClassRedeliveryRequestDto? dto = null)
    {
        await _service.RejectAsync(id, dto);
        return StatusCode(410, ApiResult<object>.Failure("410", GoneMessage));
    }
}
