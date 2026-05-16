using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OboxSteam.Application.Commons;
using OboxSteam.Application.DTOs.ModuleDTO;
using OboxSteam.Application.Interfaces;
using OboxSteam.Application.Utils;
using OboxSteam.Domain.Enums;
using Swashbuckle.AspNetCore.Annotations;

namespace OboxSteam.API.Controllers;

[Route("api/modules")]
[ApiController]
public class ModuleController : ControllerBase
{
    private readonly IModuleService _moduleService;

    public ModuleController(IModuleService moduleService)
    {
        _moduleService = moduleService;
    }

    // =========================================================================
    // GET ALL  —  GET /api/modules
    // =========================================================================

    [HttpGet]
    [SwaggerOperation(
        Summary = "Get all modules",
        Description = "Retrieve a paginated list of modules with optional search, filter, and sort options.")]
    [ProducesResponseType(typeof(ApiResult<Pagination<ModuleResponseDto>>), 200)]
    [ProducesResponseType(typeof(ApiResult<object>), 400)]
    public async Task<IActionResult> GetAllModules(
        [FromQuery, SwaggerParameter(Description = "Search by name or code (optional)")] string? search = null,
        [FromQuery, SwaggerParameter(Description = "Sort by field: name, code, moduleOrder, moduleType, price, createdAt (optional)")] string? sortBy = null,
        [FromQuery, SwaggerParameter(Description = "Sort in descending order? Default: false")] bool isDescending = false,
        [FromQuery, SwaggerParameter(Description = "Page number, starting from 1")] int page = 1,
        [FromQuery, SwaggerParameter(Description = "Number of items per page")] int pageSize = 10,
        [FromQuery, SwaggerParameter(Description = "Filter by module type (optional)")] ModuleType? moduleType = null)
    {
        if (page < 1 || pageSize < 1)
        {
            return BadRequest(ApiResult<object>.Failure("400", "Invalid pagination parameters."));
        }

        var result = await _moduleService.GetAllModulesAsync(
            search, sortBy, isDescending, page, pageSize, moduleType);

        return Ok(ApiResult<Pagination<ModuleResponseDto>>.Success(result, "200", "Modules retrieved successfully."));
    }

    // =========================================================================
    // GET BY ID  —  GET /api/modules/{id}
    // =========================================================================

    [HttpGet("{id:guid}")]
    [SwaggerOperation(
        Summary = "Get module details",
        Description = "Retrieve detailed information for a specific module by its ID.")]
    [ProducesResponseType(typeof(ApiResult<ModuleResponseDto>), 200)]
    [ProducesResponseType(typeof(ApiResult<object>), 404)]
    public async Task<IActionResult> GetModuleById([FromRoute] Guid id)
    {
        var result = await _moduleService.GetModuleByIdAsync(id);
        return Ok(ApiResult<ModuleResponseDto>.Success(result, "200", "Module retrieved successfully."));
    }

    // =========================================================================
    // GET BY NAME  —  GET /api/modules/name/{name}
    // =========================================================================

    [HttpGet("name/{name}")]
    [SwaggerOperation(
        Summary = "Get module by name",
        Description = "Retrieve a single module by its exact name (case-insensitive).")]
    [ProducesResponseType(typeof(ApiResult<ModuleResponseDto>), 200)]
    [ProducesResponseType(typeof(ApiResult<object>), 404)]
    public async Task<IActionResult> GetModuleByName(
        [FromRoute, SwaggerParameter(Description = "The module name to search for")] string name)
    {
        var result = await _moduleService.GetModuleByNameAsync(name);
        return Ok(ApiResult<ModuleResponseDto>.Success(result, "200", "Module retrieved successfully."));
    }

    // =========================================================================
    // CREATE  —  POST /api/modules          [Admin only]
    // =========================================================================

    [HttpPost]
    [Authorize(Roles = "SuperAdmin,Manager")]
    [SwaggerOperation(
        Summary = "Create a new module",
        Description = "Creates a new module with the provided information. Requires SuperAdmin or Manager role.")]
    [ProducesResponseType(typeof(ApiResult<ModuleResponseDto>), 201)]
    [ProducesResponseType(typeof(ApiResult<object>), 400)]
    [ProducesResponseType(typeof(ApiResult<object>), 401)]
    [ProducesResponseType(typeof(ApiResult<object>), 403)]
    [ProducesResponseType(typeof(ApiResult<object>), 409)]
    public async Task<IActionResult> AddModule(
        [FromBody, SwaggerParameter("New module data to be created")] ModuleCreateDto dto)
    {
        var result = await _moduleService.AddModuleAsync(dto);

        return CreatedAtAction(
            nameof(GetModuleById),
            new { id = result.Id },
            ApiResult<ModuleResponseDto>.Success(result, "201", "Module created successfully."));
    }

    // =========================================================================
    // UPDATE  —  PUT /api/modules/{id}      [Admin only]
    // =========================================================================

    [HttpPut("{id:guid}")]
    [Authorize(Roles = "SuperAdmin,Manager")]
    [SwaggerOperation(
        Summary = "Update module information",
        Description = "Updates the details of a specific module by its ID. Requires SuperAdmin or Manager role.")]
    [ProducesResponseType(typeof(ApiResult<ModuleResponseDto>), 200)]
    [ProducesResponseType(typeof(ApiResult<object>), 400)]
    [ProducesResponseType(typeof(ApiResult<object>), 401)]
    [ProducesResponseType(typeof(ApiResult<object>), 403)]
    [ProducesResponseType(typeof(ApiResult<object>), 404)]
    [ProducesResponseType(typeof(ApiResult<object>), 409)]
    public async Task<IActionResult> UpdateModule(
        [FromRoute] Guid id,
        [FromBody, SwaggerParameter("Updated module data")] ModuleUpdateDto dto)
    {
        if (dto == null)
        {
            return BadRequest(ApiResult<object>.Failure("400", "Module update data is required."));
        }

        var result = await _moduleService.UpdateModuleAsync(id, dto);
        return Ok(ApiResult<ModuleResponseDto>.Success(result, "200", "Module updated successfully."));
    }

    // =========================================================================
    // DELETE  —  DELETE /api/modules/{id}   [Admin only]
    // =========================================================================

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "SuperAdmin,Manager")]
    [SwaggerOperation(
        Summary = "Delete a module",
        Description = "Soft-deletes a module by its ID. Requires SuperAdmin or Manager role.")]
    [ProducesResponseType(typeof(ApiResult<bool>), 200)]
    [ProducesResponseType(typeof(ApiResult<object>), 401)]
    [ProducesResponseType(typeof(ApiResult<object>), 403)]
    [ProducesResponseType(typeof(ApiResult<object>), 404)]
    public async Task<IActionResult> DeleteModule([FromRoute] Guid id)
    {
        var result = await _moduleService.DeleteModuleAsync(id);

        if (!result)
        {
            return NotFound(ApiResult<object>.Failure("404", $"Module with ID '{id}' not found."));
        }

        return Ok(ApiResult<bool>.Success(result, "200", "Module deleted successfully."));
    }
}
