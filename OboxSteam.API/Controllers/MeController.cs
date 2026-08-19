using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OboxSteam.Application.DTOs.ClassEnrollmentDTO;
using OboxSteam.Application.Interfaces;
using OboxSteam.Application.Utils;
using Swashbuckle.AspNetCore.Annotations;

namespace OboxSteam.API.Controllers;

[Route("api/me")]
[ApiController]
[Authorize]
public class MeController : ControllerBase
{
    private readonly IClassEnrollmentService _classEnrollmentService;

    public MeController(IClassEnrollmentService classEnrollmentService)
    {
        _classEnrollmentService = classEnrollmentService;
    }

    [HttpGet("schedule")]
    [Authorize(Roles = "Student")]
    [SwaggerOperation(
        Summary = "Get my class schedule",
        Description = "Returns occupied session intervals for the current student's active class enrollments. Cancelled sessions are omitted.")]
    [ProducesResponseType(typeof(ApiResult<List<StudentScheduleIntervalDto>>), 200)]
    [ProducesResponseType(typeof(ApiResult<object>), 401)]
    [ProducesResponseType(typeof(ApiResult<object>), 403)]
    public async Task<IActionResult> GetMySchedule()
    {
        var result = await _classEnrollmentService.GetMyScheduleAsync();
        return Ok(ApiResult<List<StudentScheduleIntervalDto>>.Success(
            result,
            "200",
            "Schedule retrieved successfully."));
    }
}
