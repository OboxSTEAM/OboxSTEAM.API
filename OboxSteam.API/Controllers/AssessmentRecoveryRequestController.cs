using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OboxSteam.Application.DTOs.AssessmentRecoveryDTO;
using OboxSteam.Application.Interfaces;
using OboxSteam.Application.Utils;
using Swashbuckle.AspNetCore.Annotations;

namespace OboxSteam.API.Controllers;

[Route("api/assessment-recovery-requests")]
[ApiController]
public class AssessmentRecoveryRequestController : ControllerBase
{
    private readonly IAssessmentRecoveryRequestService _service;

    public AssessmentRecoveryRequestController(IAssessmentRecoveryRequestService service)
    {
        _service = service;
    }

    [HttpPost]
    [Authorize(Roles = "Student")]
    [SwaggerOperation(
        Summary = "Request extra attempts (same class window)",
        Description = "Only while the class AssignmentWindow has not ended. "
            + "409 error codes: ASSIGNMENT_WINDOW_MISSING when the student's class has no window; "
            + "ASSIGNMENT_WINDOW_CLOSED after EndTime (data = AssignmentWindowConflictDto).")]
    [ProducesResponseType(typeof(ApiResult<AssessmentRecoveryRequestResponseDto>), 201)]
    [ProducesResponseType(typeof(ApiResult<object>), 409)]
    public async Task<IActionResult> Create([FromBody] CreateAssessmentRecoveryRequestDto dto)
    {
        var result = await _service.CreateAsync(dto);
        return StatusCode(201, ApiResult<AssessmentRecoveryRequestResponseDto>.Success(
            result, "201", "Assessment recovery request created."));
    }

    [HttpGet("me")]
    [Authorize(Roles = "Student")]
    [SwaggerOperation(Summary = "List my assessment recovery requests")]
    [ProducesResponseType(typeof(ApiResult<List<AssessmentRecoveryRequestResponseDto>>), 200)]
    public async Task<IActionResult> GetMine()
    {
        var result = await _service.GetMineAsync();
        return Ok(ApiResult<List<AssessmentRecoveryRequestResponseDto>>.Success(
            result, "200", "Recovery requests retrieved."));
    }

    [HttpGet("pending")]
    [Authorize(Roles = "Mentor,Manager,Admin")]
    [SwaggerOperation(Summary = "List pending recovery requests for mentor/staff")]
    [ProducesResponseType(typeof(ApiResult<List<AssessmentRecoveryRequestResponseDto>>), 200)]
    public async Task<IActionResult> GetPending()
    {
        var result = await _service.GetPendingForMentorAsync();
        return Ok(ApiResult<List<AssessmentRecoveryRequestResponseDto>>.Success(
            result, "200", "Pending recovery requests retrieved."));
    }

    [HttpPost("{id:guid}/withdraw")]
    [Authorize(Roles = "Student")]
    [SwaggerOperation(Summary = "Withdraw a pending recovery request")]
    [ProducesResponseType(typeof(ApiResult<AssessmentRecoveryRequestResponseDto>), 200)]
    public async Task<IActionResult> Withdraw([FromRoute] Guid id)
    {
        var result = await _service.WithdrawAsync(id);
        return Ok(ApiResult<AssessmentRecoveryRequestResponseDto>.Success(
            result, "200", "Recovery request withdrawn."));
    }

    [HttpPost("{id:guid}/approve")]
    [Authorize(Roles = "Mentor,Manager,Admin")]
    [SwaggerOperation(
        Summary = "Approve recovery request (same class grant)",
        Description = "Only while the class AssignmentWindow has not ended. "
            + "409 ASSIGNMENT_WINDOW_CLOSED when the window is missing or has ended "
            + "(data = AssignmentWindowConflictDto when a window exists).")]
    [ProducesResponseType(typeof(ApiResult<AssessmentRecoveryRequestResponseDto>), 200)]
    [ProducesResponseType(typeof(ApiResult<object>), 409)]
    public async Task<IActionResult> Approve(
        [FromRoute] Guid id,
        [FromBody] DecideAssessmentRecoveryRequestDto dto)
    {
        var result = await _service.ApproveAsync(id, dto);
        return Ok(ApiResult<AssessmentRecoveryRequestResponseDto>.Success(
            result, "200", "Recovery request approved."));
    }

    [HttpPost("{id:guid}/reject")]
    [Authorize(Roles = "Mentor,Manager,Admin")]
    [SwaggerOperation(Summary = "Reject recovery request")]
    [ProducesResponseType(typeof(ApiResult<AssessmentRecoveryRequestResponseDto>), 200)]
    public async Task<IActionResult> Reject(
        [FromRoute] Guid id,
        [FromBody] DecideAssessmentRecoveryRequestDto? dto = null)
    {
        var result = await _service.RejectAsync(id, dto);
        return Ok(ApiResult<AssessmentRecoveryRequestResponseDto>.Success(
            result, "200", "Recovery request rejected."));
    }
}
