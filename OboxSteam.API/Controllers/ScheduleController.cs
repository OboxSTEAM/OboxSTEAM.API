using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OboxSteam.Application.DTOs.ScheduleDTO;
using OboxSteam.Application.Interfaces;
using OboxSteam.Application.Utils;
using Swashbuckle.AspNetCore.Annotations;

namespace OboxSteam.API.Controllers;

[Route("api/schedules")]
[ApiController]
[Authorize]
public class ScheduleController : ControllerBase
{
    private readonly IScheduleService _scheduleService;

    public ScheduleController(IScheduleService scheduleService)
    {
        _scheduleService = scheduleService;
    }

    [HttpGet("weekly")]
    [Authorize(Roles = "Student,Parent")]
    [SwaggerOperation(
        Summary = "Get weekly class timetable",
        Description = "Returns one Monday–Sunday week of class sessions in Asia/Ho_Chi_Minh, grouped by local date. Students see their own schedule. Parents must pass studentId of a verified linked child. Omit weekStart to use the current Monday. Cancelled sessions are omitted.")]
    [ProducesResponseType(typeof(ApiResult<WeeklyScheduleResponseDto>), 200)]
    [ProducesResponseType(typeof(ApiResult<object>), 400)]
    [ProducesResponseType(typeof(ApiResult<object>), 401)]
    [ProducesResponseType(typeof(ApiResult<object>), 403)]
    public async Task<IActionResult> GetWeeklySchedule(
        [FromQuery] DateOnly? weekStart = null,
        [FromQuery] Guid? studentId = null)
    {
        var result = await _scheduleService.GetWeeklyScheduleAsync(weekStart, studentId);
        return Ok(ApiResult<WeeklyScheduleResponseDto>.Success(
            result,
            "200",
            "Weekly schedule retrieved successfully."));
    }
}
