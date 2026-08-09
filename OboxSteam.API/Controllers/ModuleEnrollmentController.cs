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
    [Authorize(Roles = "Student,Parent,Admin,Manager")]
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
}
