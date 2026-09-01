using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OboxSteam.Application.Commons;
using OboxSteam.Application.DTOs.ClassSessionExpertDTO;
using OboxSteam.Application.Interfaces;
using OboxSteam.Application.Utils;
using OboxSteam.Domain.Enums;
using Swashbuckle.AspNetCore.Annotations;

namespace OboxSteam.API.Controllers;

[Route("api/class-session-experts")]
[ApiController]
public class ClassSessionExpertController : ControllerBase
{
    private readonly IClassSessionExpertService _service;

    public ClassSessionExpertController(IClassSessionExpertService service)
    {
        _service = service;
    }

    [HttpPost]
    [Authorize(Roles = "Admin,Manager")]
    [SwaggerOperation(
        Summary = "Invite an expert to co-teach an Offline session",
        Description = "One Invited or Accepted expert per session. The expert must be on the class program board.")]
    [ProducesResponseType(typeof(ApiResult<ClassSessionExpertResponseDto>), 201)]
    public async Task<IActionResult> Invite([FromBody] InviteClassSessionExpertDto dto)
    {
        var result = await _service.InviteAsync(dto);
        return StatusCode(
            StatusCodes.Status201Created,
            ApiResult<ClassSessionExpertResponseDto>.Success(result, "201", "Expert invited successfully."));
    }

    [HttpGet("mine")]
    [Authorize(Roles = "Expert")]
    [SwaggerOperation(Summary = "List my co-teach invitations")]
    [ProducesResponseType(typeof(ApiResult<Pagination<ClassSessionExpertResponseDto>>), 200)]
    public async Task<IActionResult> GetMine(
        [FromQuery] ClassSessionExpertStatus? status = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10)
    {
        if (page < 1 || pageSize < 1)
        {
            return BadRequest(ApiResult<object>.Failure("400", "Invalid pagination parameters."));
        }

        var result = await _service.GetMineAsync(status, page, pageSize);
        return Ok(ApiResult<Pagination<ClassSessionExpertResponseDto>>.Success(
            result, "200", "Invitations retrieved successfully."));
    }

    [HttpGet]
    [Authorize(Roles = "Admin,Manager")]
    [SwaggerOperation(Summary = "List co-teach invitations (manager)")]
    [ProducesResponseType(typeof(ApiResult<Pagination<ClassSessionExpertResponseDto>>), 200)]
    public async Task<IActionResult> GetInvitations(
        [FromQuery] Guid? classId = null,
        [FromQuery] Guid? sessionId = null,
        [FromQuery] Guid? expertId = null,
        [FromQuery] ClassSessionExpertStatus? status = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10)
    {
        if (page < 1 || pageSize < 1)
        {
            return BadRequest(ApiResult<object>.Failure("400", "Invalid pagination parameters."));
        }

        var result = await _service.GetForManagerAsync(classId, sessionId, expertId, status, page, pageSize);
        return Ok(ApiResult<Pagination<ClassSessionExpertResponseDto>>.Success(
            result, "200", "Invitations retrieved successfully."));
    }

    [HttpGet("{id:guid}")]
    [Authorize(Roles = "Admin,Manager,Expert")]
    [SwaggerOperation(Summary = "Get a co-teach invitation")]
    [ProducesResponseType(typeof(ApiResult<ClassSessionExpertResponseDto>), 200)]
    public async Task<IActionResult> GetById([FromRoute] Guid id)
    {
        var result = await _service.GetByIdAsync(id);
        return Ok(ApiResult<ClassSessionExpertResponseDto>.Success(
            result, "200", "Invitation retrieved successfully."));
    }

    [HttpPost("{id:guid}/accept")]
    [Authorize(Roles = "Expert")]
    [SwaggerOperation(Summary = "Accept a co-teach invitation")]
    [ProducesResponseType(typeof(ApiResult<ClassSessionExpertResponseDto>), 200)]
    public async Task<IActionResult> Accept([FromRoute] Guid id)
    {
        var result = await _service.AcceptAsync(id);
        return Ok(ApiResult<ClassSessionExpertResponseDto>.Success(
            result, "200", "Invitation accepted successfully."));
    }

    [HttpPost("{id:guid}/decline")]
    [Authorize(Roles = "Expert")]
    [SwaggerOperation(Summary = "Decline a co-teach invitation")]
    [ProducesResponseType(typeof(ApiResult<ClassSessionExpertResponseDto>), 200)]
    public async Task<IActionResult> Decline([FromRoute] Guid id)
    {
        var result = await _service.DeclineAsync(id);
        return Ok(ApiResult<ClassSessionExpertResponseDto>.Success(
            result, "200", "Invitation declined successfully."));
    }

    [HttpPost("{id:guid}/withdraw")]
    [Authorize(Roles = "Admin,Manager")]
    [SwaggerOperation(
        Summary = "Withdraw an Invited co-teach invitation",
        Description = "Allowed only while the expert has not Accepted.")]
    [ProducesResponseType(typeof(ApiResult<bool>), 200)]
    public async Task<IActionResult> Withdraw([FromRoute] Guid id)
    {
        await _service.WithdrawAsync(id);
        return Ok(ApiResult<bool>.Success(true, "200", "Invitation withdrawn successfully."));
    }

    [HttpPost("{id:guid}/approve-reschedule")]
    [Authorize(Roles = "Expert")]
    [SwaggerOperation(Summary = "Approve a pending session reschedule")]
    [ProducesResponseType(typeof(ApiResult<ClassSessionExpertResponseDto>), 200)]
    public async Task<IActionResult> ApproveReschedule([FromRoute] Guid id)
    {
        var result = await _service.ApproveRescheduleAsync(id);
        return Ok(ApiResult<ClassSessionExpertResponseDto>.Success(
            result, "200", "Reschedule approved successfully."));
    }

    [HttpPost("{id:guid}/decline-reschedule")]
    [Authorize(Roles = "Expert")]
    [SwaggerOperation(
        Summary = "Decline a pending session reschedule",
        Description = "Keeps the committed session time and the expert Accepted.")]
    [ProducesResponseType(typeof(ApiResult<ClassSessionExpertResponseDto>), 200)]
    public async Task<IActionResult> DeclineReschedule([FromRoute] Guid id)
    {
        var result = await _service.DeclineRescheduleAsync(id);
        return Ok(ApiResult<ClassSessionExpertResponseDto>.Success(
            result, "200", "Reschedule declined successfully."));
    }

    [HttpPut("{id:guid}/feedback")]
    [Authorize(Roles = "Expert")]
    [SwaggerOperation(
        Summary = "Submit or update private mentor feedback",
        Description = "Allowed after the Offline session is Completed and this expert has Accepted. "
            + "One overview for the class focused on how the mentor taught. Students never see this.")]
    [ProducesResponseType(typeof(ApiResult<ClassSessionExpertResponseDto>), 200)]
    public async Task<IActionResult> SubmitFeedback(
        [FromRoute] Guid id,
        [FromBody] SubmitClassSessionExpertFeedbackDto dto)
    {
        var result = await _service.SubmitFeedbackAsync(id, dto);
        return Ok(ApiResult<ClassSessionExpertResponseDto>.Success(
            result, "200", "Mentor feedback saved successfully."));
    }
}
