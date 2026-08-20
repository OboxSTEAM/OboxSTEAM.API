using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OboxSteam.Application.DTOs.ClassSessionDTO;
using OboxSteam.Application.DTOs.SessionAttendanceDTO;
using OboxSteam.Application.Interfaces;
using OboxSteam.Application.Utils;
using Swashbuckle.AspNetCore.Annotations;

namespace OboxSteam.API.Controllers;

/// <summary>
/// QR / fallback-code check-in for Offline class sessions.
/// The mentor projects a rotating QR (60s TTL); students scan it (mobile) or type
/// the 6-digit code (web) to mark themselves Present.
/// </summary>
[Route("api/class-sessions")]
[ApiController]
[Authorize]
public class ClassSessionCheckInController : ControllerBase
{
    private readonly ISessionAttendanceService _sessionAttendanceService;

    public ClassSessionCheckInController(ISessionAttendanceService sessionAttendanceService)
    {
        _sessionAttendanceService = sessionAttendanceService;
    }

    // =========================================================================
    // GENERATE / ROTATE TOKEN  —  POST /api/class-sessions/{id}/checkin-token
    // =========================================================================

    [HttpPost("{id:guid}/checkin-token")]
    [Authorize(Roles = "Mentor,Manager,Admin")]
    [SwaggerOperation(
        Summary = "Generate or rotate the check-in QR token",
        Description = "Generates a fresh QR token and 6-digit fallback code for the session. "
            + "The pair expires after 60 seconds — call again to rotate. "
            + "Only the assigned mentor (or Manager/Admin) may generate.")]
    [ProducesResponseType(typeof(ApiResult<ClassSessionCheckInTokenResponseDto>), 200)]
    [ProducesResponseType(typeof(ApiResult<object>), 400)]
    [ProducesResponseType(typeof(ApiResult<object>), 401)]
    [ProducesResponseType(typeof(ApiResult<object>), 403)]
    [ProducesResponseType(typeof(ApiResult<object>), 404)]
    public async Task<IActionResult> GenerateCheckInToken([FromRoute] Guid id)
    {
        var result = await _sessionAttendanceService.GenerateCheckInTokenAsync(id);

        return Ok(ApiResult<ClassSessionCheckInTokenResponseDto>.Success(
            result,
            "200",
            "Check-in token generated successfully."));
    }

    // =========================================================================
    // STUDENT CHECK-IN  —  POST /api/class-sessions/{id}/checkin
    // =========================================================================

    [HttpPost("{id:guid}/checkin")]
    [Authorize(Roles = "Student")]
    [SwaggerOperation(
        Summary = "Check in to a session",
        Description = "Student self check-in using the QR token (mobile scan) or the 6-digit "
            + "fallback code (web manual entry). Records attendance as Present.")]
    [ProducesResponseType(typeof(ApiResult<SessionAttendanceResponseDto>), 200)]
    [ProducesResponseType(typeof(ApiResult<object>), 400)]
    [ProducesResponseType(typeof(ApiResult<object>), 401)]
    [ProducesResponseType(typeof(ApiResult<object>), 403)]
    [ProducesResponseType(typeof(ApiResult<object>), 404)]
    public async Task<IActionResult> CheckIn(
        [FromRoute] Guid id,
        [FromBody, SwaggerParameter("QR token or 6-digit fallback code")] ClassSessionCheckInRequestDto dto)
    {
        if (dto == null)
        {
            return BadRequest(ApiResult<object>.Failure("400", "Check-in data is required."));
        }

        var result = await _sessionAttendanceService.CheckInAsync(id, dto);

        return Ok(ApiResult<SessionAttendanceResponseDto>.Success(
            result,
            "200",
            "Checked in successfully."));
    }
}
