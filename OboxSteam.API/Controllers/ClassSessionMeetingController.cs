using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OboxSteam.Application.DTOs.ClassSessionDTO;
using OboxSteam.Application.Interfaces;
using OboxSteam.Application.Utils;
using Swashbuckle.AspNetCore.Annotations;

namespace OboxSteam.API.Controllers;

/// <summary>JaaS LiveOnline join/leave with server-side attendance.</summary>
[Route("api/class-sessions")]
[ApiController]
[Authorize]
public class ClassSessionMeetingController : ControllerBase
{
    private readonly ISessionMeetingService _sessionMeetingService;

    public ClassSessionMeetingController(ISessionMeetingService sessionMeetingService)
    {
        _sessionMeetingService = sessionMeetingService;
    }

    [HttpPost("{id:guid}/join")]
    [Authorize(Roles = "Student,Mentor,Manager,Admin")]
    [SwaggerOperation(
        Summary = "Join a LiveOnline meeting",
        Description = "Records student attendance (Present within 10' grace, otherwise Late), "
            + "and returns a JaaS JWT + room credentials. Mentors receive moderator:true. "
            + "Join window opens 15 minutes before StartTime and closes at EndTime.")]
    [ProducesResponseType(typeof(ApiResult<ClassSessionJoinResponseDto>), 200)]
    [ProducesResponseType(typeof(ApiResult<object>), 400)]
    [ProducesResponseType(typeof(ApiResult<object>), 401)]
    [ProducesResponseType(typeof(ApiResult<object>), 403)]
    [ProducesResponseType(typeof(ApiResult<object>), 404)]
    public async Task<IActionResult> Join([FromRoute] Guid id)
    {
        var result = await _sessionMeetingService.JoinAsync(id);
        return Ok(ApiResult<ClassSessionJoinResponseDto>.Success(
            result,
            "200",
            "Joined meeting successfully."));
    }

    [HttpPost("{id:guid}/leave")]
    [Authorize(Roles = "Student,Mentor,Manager,Admin")]
    [SwaggerOperation(
        Summary = "Leave a LiveOnline meeting",
        Description = "For students, sets LeftAt and ParticipationMinutes from CheckedInAt. "
            + "Mentors/managers leave without attendance mutation.")]
    [ProducesResponseType(typeof(ApiResult<ClassSessionLeaveResponseDto>), 200)]
    [ProducesResponseType(typeof(ApiResult<object>), 400)]
    [ProducesResponseType(typeof(ApiResult<object>), 401)]
    [ProducesResponseType(typeof(ApiResult<object>), 403)]
    [ProducesResponseType(typeof(ApiResult<object>), 404)]
    public async Task<IActionResult> Leave([FromRoute] Guid id)
    {
        var result = await _sessionMeetingService.LeaveAsync(id);
        return Ok(ApiResult<ClassSessionLeaveResponseDto>.Success(
            result,
            "200",
            "Left meeting successfully."));
    }
}
