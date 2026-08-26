using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OboxSteam.Application.DTOs.ClassRedeliveryDTO;
using OboxSteam.Application.Interfaces;
using OboxSteam.Application.Utils;
using Swashbuckle.AspNetCore.Annotations;

namespace OboxSteam.API.Controllers;

[Route("api/class-redelivery-requests")]
[ApiController]
public class ClassRedeliveryRequestController : ControllerBase
{
    private readonly IClassRedeliveryRequestService _service;

    public ClassRedeliveryRequestController(IClassRedeliveryRequestService service)
    {
        _service = service;
    }

    [HttpPost]
    [Authorize(Roles = "Student,Mentor,Manager,Admin")]
    [SwaggerOperation(Summary = "Request class re-delivery (auto-match or manager queue)")]
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
    [SwaggerOperation(Summary = "Eligible classes the student can pick for re-delivery (tier 1)")]
    [ProducesResponseType(typeof(ApiResult<List<ClassRedeliveryCandidateDto>>), 200)]
    public async Task<IActionResult> GetCandidates([FromRoute] Guid id)
    {
        var result = await _service.GetCandidatesAsync(id);
        return Ok(ApiResult<List<ClassRedeliveryCandidateDto>>.Success(
            result, "200", "Candidate classes retrieved."));
    }

    [HttpPost("{id:guid}/select-class")]
    [Authorize(Roles = "Student")]
    [SwaggerOperation(Summary = "Student picks a class for re-delivery (then pays the program price)")]
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
    [SwaggerOperation(Summary = "Student accepts the intensive remedial-class schedule (tier 2)")]
    [ProducesResponseType(typeof(ApiResult<ClassRedeliveryRequestResponseDto>), 200)]
    public async Task<IActionResult> AcceptIntensive([FromRoute] Guid id)
    {
        var result = await _service.AcceptIntensiveAsync(id);
        return Ok(ApiResult<ClassRedeliveryRequestResponseDto>.Success(
            result, "200", "Intensive schedule accepted; payment pending."));
    }

    [HttpPost("{id:guid}/decline-intensive")]
    [Authorize(Roles = "Student")]
    [SwaggerOperation(Summary = "Student declines the remedial-class offer (progress is kept)")]
    [ProducesResponseType(typeof(ApiResult<ClassRedeliveryRequestResponseDto>), 200)]
    public async Task<IActionResult> DeclineIntensive([FromRoute] Guid id)
    {
        var result = await _service.DeclineIntensiveAsync(id);
        return Ok(ApiResult<ClassRedeliveryRequestResponseDto>.Success(
            result, "200", "Remedial class offer declined."));
    }

    [HttpGet("pending-manager")]
    [Authorize(Roles = "Manager,Admin")]
    [SwaggerOperation(Summary = "Manager queue when no eligible class was auto-matched")]
    [ProducesResponseType(typeof(ApiResult<List<ClassRedeliveryRequestResponseDto>>), 200)]
    public async Task<IActionResult> GetPendingManager()
    {
        var result = await _service.GetPendingManagerAsync();
        return Ok(ApiResult<List<ClassRedeliveryRequestResponseDto>>.Success(
            result, "200", "Pending manager re-delivery requests retrieved."));
    }

    [HttpPost("{id:guid}/withdraw")]
    [Authorize(Roles = "Student,Mentor,Manager,Admin")]
    [SwaggerOperation(Summary = "Withdraw an open re-delivery request")]
    [ProducesResponseType(typeof(ApiResult<ClassRedeliveryRequestResponseDto>), 200)]
    public async Task<IActionResult> Withdraw([FromRoute] Guid id)
    {
        var result = await _service.WithdrawAsync(id);
        return Ok(ApiResult<ClassRedeliveryRequestResponseDto>.Success(
            result, "200", "Re-delivery request withdrawn."));
    }

    [HttpPost("{id:guid}/assign-target")]
    [Authorize(Roles = "Manager,Admin")]
    [SwaggerOperation(Summary = "Manager assigns a target class (then student pays program price for re-delivery)")]
    [ProducesResponseType(typeof(ApiResult<ClassRedeliveryRequestResponseDto>), 200)]
    public async Task<IActionResult> AssignTarget(
        [FromRoute] Guid id,
        [FromBody] DecideClassRedeliveryRequestDto dto)
    {
        var result = await _service.ManagerAssignTargetAsync(id, dto);
        return Ok(ApiResult<ClassRedeliveryRequestResponseDto>.Success(
            result, "200", "Target class assigned; payment pending."));
    }

    [HttpPost("{id:guid}/reject")]
    [Authorize(Roles = "Manager,Admin")]
    [SwaggerOperation(Summary = "Reject a re-delivery request")]
    [ProducesResponseType(typeof(ApiResult<ClassRedeliveryRequestResponseDto>), 200)]
    public async Task<IActionResult> Reject(
        [FromRoute] Guid id,
        [FromBody] DecideClassRedeliveryRequestDto? dto = null)
    {
        var result = await _service.RejectAsync(id, dto);
        return Ok(ApiResult<ClassRedeliveryRequestResponseDto>.Success(
            result, "200", "Re-delivery request rejected."));
    }
}
