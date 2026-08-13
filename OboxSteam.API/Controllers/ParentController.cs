using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OboxSteam.Application.DTOs.AuthDTO;
using OboxSteam.Application.DTOs.ParentDTO;
using OboxSteam.Application.DTOs.ParentProgressionDTO;
using OboxSteam.Application.Interfaces;
using OboxSteam.Application.Utils;
using Swashbuckle.AspNetCore.Annotations;

namespace OboxSteam.API.Controllers;

[Route("api/parent")]
[ApiController]
public class ParentController : ControllerBase
{
    private readonly IParentService _parentService;
    private readonly IParentProgressionService _parentProgressionService;
    private readonly IConfiguration _configuration;

    public ParentController(
        IParentService parentService,
        IParentProgressionService parentProgressionService,
        IConfiguration configuration)
    {
        _parentService = parentService;
        _parentProgressionService = parentProgressionService;
        _configuration = configuration;
    }

    [HttpPost("request-link")]
    [Authorize(Roles = "Student")] // Chỉ dành cho học sinh
    public async Task<IActionResult> RequestLink([FromBody] RequestLinkDto dto)
    {
        var result = await _parentService.RequestParentLinkAsync(dto, _configuration);
        return Ok(ApiResult<object>.Success(result, "200", "Parent link request sent successfully."));
    }

    [HttpPost("magic-login")]
    [AllowAnonymous]
    public async Task<IActionResult> MagicLogin([FromBody] MagicLoginDto dto)
    {
        var result = await _parentService.MagicLoginAsync(dto, _configuration);
        return Ok(ApiResult<LoginResponseDto>.Success(result, "200", "Logged in via Magic Link and confirmed association successfully."));
    }

    [HttpPost("complete-profile")]
    [Authorize(Roles = "Parent")]
    public async Task<IActionResult> CompleteProfile([FromBody] CompleteProfileDto dto)
    {
        var result = await _parentService.CompleteProfileAsync(dto);
        return Ok(ApiResult<object>.Success(result, "200", "Profile completed and password created successfully."));
    }

    [HttpPost("approve-link")]
    [Authorize(Roles = "Parent")]
    public async Task<IActionResult> ApproveLink([FromBody] ApproveLinkDto dto)
    {
        var result = await _parentService.ApproveLinkAsync(dto, _configuration);
        return Ok(ApiResult<object>.Success(result, "200", "Student association approved successfully."));
    }

    [HttpGet("links")]
    [Authorize(Roles = "Student,Parent")]
    public async Task<IActionResult> GetParentStudentRelations()
    {
        var result = await _parentService.GetParentStudentRelationsAsync();
        return Ok(ApiResult<List<ParentStudentRelationDto>>.Success(result, "200", "Retrieved associated accounts successfully."));
    }

    [HttpGet("children/{studentId:guid}/progression")]
    [Authorize(Roles = "Parent")]
    [SwaggerOperation(
        Summary = "Get linked child progression brief",
        Description = "Parent-only summary for a verified linked student: enrollments, current stage, blockers, and recent milestones.")]
    [ProducesResponseType(typeof(ApiResult<ParentChildProgressionDto>), 200)]
    [ProducesResponseType(typeof(ApiResult<object>), 401)]
    [ProducesResponseType(typeof(ApiResult<object>), 403)]
    [ProducesResponseType(typeof(ApiResult<object>), 404)]
    public async Task<IActionResult> GetChildProgression(Guid studentId)
    {
        var result = await _parentProgressionService.GetChildProgressionAsync(studentId);
        return Ok(ApiResult<ParentChildProgressionDto>.Success(
            result,
            "200",
            "Child progression retrieved successfully."));
    }

    [HttpGet("children/{studentId:guid}/enrollments/{enrollmentId:guid}/progression")]
    [Authorize(Roles = "Parent")]
    [SwaggerOperation(
        Summary = "Get program progression drill-down for a linked child",
        Description = "Parent-only module timeline and assignment outcomes for one program enrollment. No curriculum content or resume state.")]
    [ProducesResponseType(typeof(ApiResult<ParentEnrollmentProgressionDto>), 200)]
    [ProducesResponseType(typeof(ApiResult<object>), 401)]
    [ProducesResponseType(typeof(ApiResult<object>), 403)]
    [ProducesResponseType(typeof(ApiResult<object>), 404)]
    public async Task<IActionResult> GetEnrollmentProgression(Guid studentId, Guid enrollmentId)
    {
        var result = await _parentProgressionService.GetEnrollmentProgressionAsync(studentId, enrollmentId);
        return Ok(ApiResult<ParentEnrollmentProgressionDto>.Success(
            result,
            "200",
            "Enrollment progression retrieved successfully."));
    }
}
