using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OboxSteam.Application.Commons;
using OboxSteam.Application.DTOs.ClassDTO;
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
    private readonly IRebuyClassCatalogService _rebuyClassCatalogService;

    public ModuleEnrollmentController(
        IModuleEnrollmentService moduleEnrollmentService,
        IRebuyClassCatalogService rebuyClassCatalogService)
    {
        _moduleEnrollmentService = moduleEnrollmentService;
        _rebuyClassCatalogService = rebuyClassCatalogService;
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

    // =========================================================================
    // CONTINUITY CLASSES  —  GET /api/module-enrollments/{id}/continuity-classes
    // =========================================================================

    [HttpGet("{id:guid}/continuity-classes")]
    [Authorize(Roles = "Student")]
    [SwaggerOperation(
        Summary = "Continuity class catalog for an Active module enrollment",
        Description = "Same RebuyClassCatalogDto shape as rebuy-classes. Use while the program enrollment is still Active; after fail/drop use GET /api/programs/{id}/rebuy-classes.")]
    [ProducesResponseType(typeof(ApiResult<RebuyClassCatalogDto>), 200)]
    [ProducesResponseType(typeof(ApiResult<object>), 400)]
    [ProducesResponseType(typeof(ApiResult<object>), 403)]
    [ProducesResponseType(typeof(ApiResult<object>), 404)]
    public async Task<IActionResult> GetContinuityClasses([FromRoute] Guid id)
    {
        var result = await _rebuyClassCatalogService.GetContinuityClassesForModuleEnrollmentAsync(id);
        return Ok(ApiResult<RebuyClassCatalogDto>.Success(
            result,
            "200",
            "Continuity class catalog retrieved."));
    }
}
