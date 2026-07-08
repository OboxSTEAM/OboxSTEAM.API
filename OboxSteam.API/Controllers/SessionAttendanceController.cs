using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OboxSteam.Application.Commons;
using OboxSteam.Application.DTOs.SessionAttendanceDTO;
using OboxSteam.Application.Interfaces;
using OboxSteam.Application.Utils;
using OboxSteam.Domain.Enums;
using Swashbuckle.AspNetCore.Annotations;

namespace OboxSteam.API.Controllers;

[Route("api/classes/{classId:guid}/sessions/{sessionId:guid}/attendance")]
[ApiController]
public sealed class SessionAttendanceController : ControllerBase
{
    private readonly ISessionAttendanceService _sessionAttendanceService;
    private readonly IClassSessionService _classSessionService;

    public SessionAttendanceController(
        ISessionAttendanceService sessionAttendanceService,
        IClassSessionService classSessionService)
    {
        _sessionAttendanceService = sessionAttendanceService;
        _classSessionService = classSessionService;
    }

    // =========================================================================
    // GET ALL  —  GET /api/classes/{classId}/sessions/{sessionId}/attendance
    // =========================================================================

    [HttpGet]
    [Authorize(Roles = "Student,Mentor,Manager,SuperAdmin")]
    [SwaggerOperation(
        Summary = "Get session attendance roster",
        Description = "Returns attendance records for a class session. Students receive only their own record; "
            + "mentors, managers, and super admins receive the full roster.")]
    [ProducesResponseType(typeof(ApiResult<Pagination<SessionAttendanceResponseDto>>), 200)]
    [ProducesResponseType(typeof(ApiResult<object>), 400)]
    [ProducesResponseType(typeof(ApiResult<object>), 401)]
    [ProducesResponseType(typeof(ApiResult<object>), 403)]
    [ProducesResponseType(typeof(ApiResult<object>), 404)]
    public async Task<IActionResult> GetSessionAttendances(
        [FromRoute] Guid classId,
        [FromRoute] Guid sessionId,
        [FromQuery, SwaggerParameter(Description = "Sort by: status, checkedInAt, studentId, createdAt")] string? sortBy = null,
        [FromQuery, SwaggerParameter(Description = "Sort in descending order? Default: false")] bool isDescending = false,
        [FromQuery, SwaggerParameter(Description = "Page number, starting from 1")] int page = 1,
        [FromQuery, SwaggerParameter(Description = "Number of items per page")] int pageSize = 10,
        [FromQuery, SwaggerParameter(Description = "Filter by attendance status (optional)")] AttendanceStatus? status = null,
        [FromQuery, SwaggerParameter(Description = "Filter by student ID (optional)")] Guid? studentId = null)
    {
        if (page < 1 || pageSize < 1)
        {
            return BadRequest(ApiResult<object>.Failure("400", "Invalid pagination parameters."));
        }

        var classSession = await _classSessionService.GetClassSessionByIdAsync(sessionId);
        if (classSession.ClassId != classId)
        {
            return NotFound(ApiResult<object>.Failure("404", $"Class session with ID '{sessionId}' not found."));
        }

        var result = await _sessionAttendanceService.GetSessionAttendancesByClassSessionIdAsync(
            sessionId,
            sortBy,
            isDescending,
            page,
            pageSize,
            status,
            studentId);

        return Ok(ApiResult<Pagination<SessionAttendanceResponseDto>>.Success(
            result,
            "200",
            "Session attendance retrieved successfully."));
    }

    // =========================================================================
    // UPDATE (UPSERT BY STUDENT)
    // PUT /api/classes/{classId}/sessions/{sessionId}/attendance/students/{studentId}
    // =========================================================================

    [HttpPut("students/{studentId:guid}")]
    [Authorize(Roles = "Mentor,Manager,SuperAdmin")]
    [SwaggerOperation(
        Summary = "Update session attendance (by student)",
        Description = "Upserts one student's attendance for a class session. Requires Mentor, Manager, or SuperAdmin role.")]
    [ProducesResponseType(typeof(ApiResult<SessionAttendanceResponseDto>), 200)]
    [ProducesResponseType(typeof(ApiResult<object>), 400)]
    [ProducesResponseType(typeof(ApiResult<object>), 401)]
    [ProducesResponseType(typeof(ApiResult<object>), 403)]
    [ProducesResponseType(typeof(ApiResult<object>), 404)]
    public async Task<IActionResult> UpdateSessionAttendance(
        [FromRoute] Guid classId,
        [FromRoute] Guid sessionId,
        [FromRoute] Guid studentId,
        [FromBody, SwaggerParameter("Attendance update data")] UpdateSessionAttendanceRequestDto dto)
    {
        if (dto == null)
        {
            return BadRequest(ApiResult<object>.Failure("400", "Session attendance update data is required."));
        }

        var result = await _sessionAttendanceService.UpdateSessionAttendanceAsync(classId, sessionId, studentId, dto);

        return Ok(ApiResult<SessionAttendanceResponseDto>.Success(
            result,
            "200",
            "Session attendance updated successfully."));
    }
}
