using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OboxSteam.Application.Commons;
using OboxSteam.Application.DTOs.EnrollmentDTO;
using OboxSteam.Application.Interfaces;
using OboxSteam.Application.Utils;
using Swashbuckle.AspNetCore.Annotations;

namespace OboxSteam.API.Controllers;

[Route("api/module-enrollments")]
[ApiController]
public class ModuleEnrollmentController : ControllerBase
{
    private readonly IModuleEnrollmentService _moduleEnrollmentService;

    public ModuleEnrollmentController(IModuleEnrollmentService moduleEnrollmentService)
    {
        _moduleEnrollmentService = moduleEnrollmentService;
    }

    // =========================================================================
    // ENROLL  —  POST /api/module-enrollments          [Student only]
    // =========================================================================

    [HttpPost]
    [Authorize(Roles = "Student")]
    [SwaggerOperation(
        Summary = "Enroll in a module",
        Description = "Creates a module enrollment within an active program enrollment. Requires Student role.")]
    [ProducesResponseType(typeof(ApiResult<ModuleEnrollmentResponseDto>), 201)]
    [ProducesResponseType(typeof(ApiResult<object>), 400)]
    [ProducesResponseType(typeof(ApiResult<object>), 401)]
    [ProducesResponseType(typeof(ApiResult<object>), 403)]
    [ProducesResponseType(typeof(ApiResult<object>), 404)]
    [ProducesResponseType(typeof(ApiResult<object>), 409)]
    public async Task<IActionResult> EnrollModule(
        [FromBody, SwaggerParameter("Module enrollment request")] CreateModuleEnrollmentRequestDto dto)
    {
        var result = await _moduleEnrollmentService.EnrollModuleAsync(dto);

        return CreatedAtAction(
            nameof(GetModuleEnrollmentById),
            new { id = result.Id },
            ApiResult<ModuleEnrollmentResponseDto>.Success(result, "201", "Module enrollment created successfully."));
    }

    // =========================================================================
    // RETAKE  —  POST /api/module-enrollments/retake   [Student only]
    // =========================================================================

    [HttpPost("retake")]
    [Authorize(Roles = "Student")]
    [SwaggerOperation(
        Summary = "Retake a module",
        Description = "Creates a new module enrollment attempt after failing the module (two failed assignments). Requires Student role.")]
    [ProducesResponseType(typeof(ApiResult<ModuleEnrollmentResponseDto>), 201)]
    [ProducesResponseType(typeof(ApiResult<object>), 400)]
    [ProducesResponseType(typeof(ApiResult<object>), 401)]
    [ProducesResponseType(typeof(ApiResult<object>), 403)]
    [ProducesResponseType(typeof(ApiResult<object>), 404)]
    [ProducesResponseType(typeof(ApiResult<object>), 409)]
    public async Task<IActionResult> RetakeModule(
        [FromBody, SwaggerParameter("Module retake request")] UpdateModuleEnrollmentRequestDto dto)
    {
        var result = await _moduleEnrollmentService.RetakeModuleAsync(dto);

        return CreatedAtAction(
            nameof(GetModuleEnrollmentById),
            new { id = result.Id },
            ApiResult<ModuleEnrollmentResponseDto>.Success(result, "201", "Module retake enrollment created successfully."));
    }

    // =========================================================================
    // GET BY ID  —  GET /api/module-enrollments/{id}
    // =========================================================================

    [HttpGet("{id:guid}")]
    [Authorize(Roles = "Student,Parent,SuperAdmin,Manager")]
    [SwaggerOperation(
        Summary = "Get module enrollment by ID",
        Description = "Retrieve a module enrollment. Students see their own; parents see linked students; admins see all.")]
    [ProducesResponseType(typeof(ApiResult<ModuleEnrollmentResponseDto>), 200)]
    [ProducesResponseType(typeof(ApiResult<object>), 401)]
    [ProducesResponseType(typeof(ApiResult<object>), 403)]
    [ProducesResponseType(typeof(ApiResult<object>), 404)]
    public async Task<IActionResult> GetModuleEnrollmentById([FromRoute] Guid id)
    {
        var result = await _moduleEnrollmentService.GetModuleEnrollmentByIdAsync(id);

        return Ok(ApiResult<ModuleEnrollmentResponseDto>.Success(
            result,
            "200",
            "Module enrollment retrieved successfully."));
    }

    // =========================================================================
    // GET BY PROGRAM ENROLLMENT  —  GET /api/module-enrollments/program-enrollment/{programEnrollmentId}
    // =========================================================================

    [HttpGet("program-enrollment/{programEnrollmentId:guid}")]
    [Authorize(Roles = "Student,Parent,SuperAdmin,Manager")]
    [SwaggerOperation(
        Summary = "Get module enrollments by program enrollment",
        Description = "Lists module enrollments for a program enrollment. Access is enforced per role.")]
    [ProducesResponseType(typeof(ApiResult<Pagination<ModuleEnrollmentResponseDto>>), 200)]
    [ProducesResponseType(typeof(ApiResult<object>), 400)]
    [ProducesResponseType(typeof(ApiResult<object>), 401)]
    [ProducesResponseType(typeof(ApiResult<object>), 403)]
    [ProducesResponseType(typeof(ApiResult<object>), 404)]
    public async Task<IActionResult> GetModuleEnrollmentsByProgramEnrollment(
        [FromRoute] Guid programEnrollmentId,
        [FromQuery, SwaggerParameter(Description = "Sort by: moduleOrder, attemptNumber, progressPercent, status, enrolledAt, createdAt")] string? sortBy = null,
        [FromQuery, SwaggerParameter(Description = "Sort in descending order? Default: false")] bool isDescending = false,
        [FromQuery, SwaggerParameter(Description = "Page number, starting from 1")] int page = 1,
        [FromQuery, SwaggerParameter(Description = "Number of items per page")] int pageSize = 10)
    {
        if (page < 1 || pageSize < 1)
        {
            return BadRequest(ApiResult<object>.Failure("400", "Invalid pagination parameters."));
        }

        var result = await _moduleEnrollmentService.GetModuleEnrollmentsByProgramEnrollmentAsync(
            programEnrollmentId,
            sortBy,
            isDescending,
            page,
            pageSize);

        return Ok(ApiResult<Pagination<ModuleEnrollmentResponseDto>>.Success(
            result,
            "200",
            "Module enrollments retrieved successfully."));
    }
}
