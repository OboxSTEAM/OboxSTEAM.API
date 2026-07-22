using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OboxSteam.Application.Commons;
using OboxSteam.Application.DTOs.ClassMentorRequestDTO;
using OboxSteam.Application.Interfaces;
using OboxSteam.Application.Utils;
using OboxSteam.Domain.Enums;
using Swashbuckle.AspNetCore.Annotations;

namespace OboxSteam.API.Controllers;

[Route("api/class-mentor-requests")]
[ApiController]
public class ClassMentorRequestController : ControllerBase
{
    private readonly IClassMentorRequestService _service;

    public ClassMentorRequestController(IClassMentorRequestService service)
    {
        _service = service;
    }

    [HttpGet("board")]
    [Authorize(Roles = "Mentor")]
    [SwaggerOperation(
        Summary = "Mentor board of available classes",
        Description = "Lists Draft/Open classes with no assigned mentor that mentors can request.")]
    [ProducesResponseType(typeof(ApiResult<Pagination<ClassMentorBoardItemDto>>), 200)]
    public async Task<IActionResult> GetMentorBoard(
        [FromQuery] string? search = null,
        [FromQuery] string? sortBy = null,
        [FromQuery] bool isDescending = false,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] Guid? programId = null,
        [FromQuery, SwaggerParameter(Description = "When true, only classes that share at least one RequiredSkill with the mentor. Default: false (show all).")] bool matchMySkills = false)
    {
        if (page < 1 || pageSize < 1)
        {
            return BadRequest(ApiResult<object>.Failure("400", "Invalid pagination parameters."));
        }

        var result = await _service.GetMentorBoardAsync(
            search, sortBy, isDescending, page, pageSize, programId, matchMySkills);

        return Ok(ApiResult<Pagination<ClassMentorBoardItemDto>>.Success(
            result, "200", "Mentor board retrieved successfully."));
    }

    [HttpPost]
    [Authorize(Roles = "Mentor")]
    [SwaggerOperation(Summary = "Submit a class assignment request")]
    [ProducesResponseType(typeof(ApiResult<ClassMentorRequestResponseDto>), 201)]
    public async Task<IActionResult> CreateRequest([FromBody] CreateClassMentorRequestDto dto)
    {
        var result = await _service.CreateRequestAsync(dto);
        return StatusCode(
            StatusCodes.Status201Created,
            ApiResult<ClassMentorRequestResponseDto>.Success(result, "201", "Request submitted successfully."));
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "Mentor")]
    [SwaggerOperation(Summary = "Withdraw own pending request")]
    [ProducesResponseType(typeof(ApiResult<bool>), 200)]
    public async Task<IActionResult> WithdrawRequest([FromRoute] Guid id)
    {
        await _service.WithdrawRequestAsync(id);
        return Ok(ApiResult<bool>.Success(true, "200", "Request withdrawn successfully."));
    }

    [HttpGet("mine")]
    [Authorize(Roles = "Mentor")]
    [SwaggerOperation(Summary = "List my class mentor requests")]
    [ProducesResponseType(typeof(ApiResult<Pagination<ClassMentorRequestResponseDto>>), 200)]
    public async Task<IActionResult> GetMyRequests(
        [FromQuery] ClassMentorRequestStatus? status = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10)
    {
        if (page < 1 || pageSize < 1)
        {
            return BadRequest(ApiResult<object>.Failure("400", "Invalid pagination parameters."));
        }

        var result = await _service.GetMyRequestsAsync(status, page, pageSize);
        return Ok(ApiResult<Pagination<ClassMentorRequestResponseDto>>.Success(
            result, "200", "Requests retrieved successfully."));
    }

    [HttpGet]
    [Authorize(Roles = "SuperAdmin,Manager")]
    [SwaggerOperation(Summary = "List class mentor requests (manager)")]
    [ProducesResponseType(typeof(ApiResult<Pagination<ClassMentorRequestResponseDto>>), 200)]
    public async Task<IActionResult> GetRequests(
        [FromQuery] Guid? classId = null,
        [FromQuery] Guid? mentorId = null,
        [FromQuery] ClassMentorRequestStatus? status = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10)
    {
        if (page < 1 || pageSize < 1)
        {
            return BadRequest(ApiResult<object>.Failure("400", "Invalid pagination parameters."));
        }

        var result = await _service.GetRequestsForManagerAsync(classId, mentorId, status, page, pageSize);
        return Ok(ApiResult<Pagination<ClassMentorRequestResponseDto>>.Success(
            result, "200", "Requests retrieved successfully."));
    }

    [HttpPost("{id:guid}/approve")]
    [Authorize(Roles = "SuperAdmin,Manager")]
    [SwaggerOperation(Summary = "Approve a pending mentor request")]
    [ProducesResponseType(typeof(ApiResult<ClassMentorRequestResponseDto>), 200)]
    public async Task<IActionResult> ApproveRequest(
        [FromRoute] Guid id,
        [FromBody] DecideClassMentorRequestDto? dto = null)
    {
        var result = await _service.ApproveRequestAsync(id, dto);
        return Ok(ApiResult<ClassMentorRequestResponseDto>.Success(
            result, "200", "Request approved successfully."));
    }

    [HttpPost("{id:guid}/reject")]
    [Authorize(Roles = "SuperAdmin,Manager")]
    [SwaggerOperation(Summary = "Reject a pending mentor request")]
    [ProducesResponseType(typeof(ApiResult<ClassMentorRequestResponseDto>), 200)]
    public async Task<IActionResult> RejectRequest(
        [FromRoute] Guid id,
        [FromBody] DecideClassMentorRequestDto? dto = null)
    {
        var result = await _service.RejectRequestAsync(id, dto);
        return Ok(ApiResult<ClassMentorRequestResponseDto>.Success(
            result, "200", "Request rejected successfully."));
    }
}
