using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OboxSteam.Application.Commons;
using OboxSteam.Application.DTOs.ProgramDTO;
using OboxSteam.Application.Interfaces;
using OboxSteam.Application.Utils;
using OboxSteam.Domain.Enums;
using Swashbuckle.AspNetCore.Annotations;

namespace OboxSteam.API.Controllers;

[Route("api/programs")]
[ApiController]
public class ProgramController : ControllerBase
{
    private readonly IProgramService _programService;

    public ProgramController(IProgramService programService)
    {
        _programService = programService;
    }

    // =========================================================================
    // GET ALL  —  GET /api/programs
    // =========================================================================

    [HttpGet]
    [SwaggerOperation(
        Summary = "Get all programs",
        Description = "Retrieve a paginated list of programs with optional search, filter, and sort options.")]
    [ProducesResponseType(typeof(ApiResult<Pagination<ProgramsResponseDto>>), 200)]
    [ProducesResponseType(typeof(ApiResult<object>), 400)]
    [ProducesResponseType(typeof(ApiResult<object>), 500)]
    public async Task<IActionResult> GetAllPrograms(
        [FromQuery, SwaggerParameter(Description = "Search by name or code (optional)")] string? search = null,
        [FromQuery, SwaggerParameter(Description = "Sort by field: name, code, level, rating, price, createdAt (optional)")] string? sortBy = null,
        [FromQuery, SwaggerParameter(Description = "Sort in descending order? Default: false")] bool isDescending = false,
        [FromQuery, SwaggerParameter(Description = "Page number, starting from 1")] int page = 1,
        [FromQuery, SwaggerParameter(Description = "Number of items per page")] int pageSize = 10,
        [FromQuery, SwaggerParameter(Description = "Filter by program code (optional)")] string? code = null,
        [FromQuery, SwaggerParameter(Description = "Filter by difficulty level (optional)")] DifficultyLevel? level = null,
        [FromQuery, SwaggerParameter(Description = "Filter by minimum rating (optional)")] decimal? rating = null,
        [FromQuery, SwaggerParameter(Description = "Filter by skills gained keyword (optional)")] string? skillsGained = null,
        [FromQuery, SwaggerParameter(Description = "Filter by program status (optional)")] string? status = null)
    {
        if (page < 1 || pageSize < 1)
            return BadRequest(ApiResult<object>.Failure("400", "Invalid pagination parameters."));

        var result = await _programService.GetAllProgramAsync(
            search, sortBy, isDescending, page, pageSize,
            code, level, rating, skillsGained, status);

        return Ok(ApiResult<Pagination<ProgramsResponseDto>>.Success(result, "200", "Programs retrieved successfully."));
    }

    // =========================================================================
    // GET BY ID  —  GET /api/programs/{id}
    // =========================================================================

    [HttpGet("{id:guid}")]
    [SwaggerOperation(
        Summary = "Get program details",
        Description = "Retrieve detailed information for a specific program by its ID.")]
    [ProducesResponseType(typeof(ApiResult<ProgramsResponseDto>), 200)]
    [ProducesResponseType(typeof(ApiResult<object>), 404)]
    [ProducesResponseType(typeof(ApiResult<object>), 500)]
    public async Task<IActionResult> GetProgramById([FromRoute] Guid id)
    {
        var result = await _programService.GetProgramByIdAsync(id);
        return Ok(ApiResult<ProgramsResponseDto>.Success(result, "200", "Program retrieved successfully."));
    }

    // =========================================================================
    // GET BY NAME  —  GET /api/programs/name/{name}
    // =========================================================================

    [HttpGet("name/{name}")]
    [SwaggerOperation(
        Summary = "Get program by name",
        Description = "Retrieve a single program by its exact name (case-insensitive).")]
    [ProducesResponseType(typeof(ApiResult<ProgramsResponseDto>), 200)]
    [ProducesResponseType(typeof(ApiResult<object>), 404)]
    [ProducesResponseType(typeof(ApiResult<object>), 500)]
    public async Task<IActionResult> GetProgramByName(
        [FromRoute, SwaggerParameter(Description = "The program name to search for")] string name)
    {
        var result = await _programService.GetProgramByNameAsync(name);
        return Ok(ApiResult<ProgramsResponseDto>.Success(result, "200", "Program retrieved successfully."));
    }

    // =========================================================================
    // CREATE  —  POST /api/programs          [Admin only]
    // =========================================================================

    [HttpPost]
    [Authorize(Roles = "SuperAdmin,Manager")]
    [SwaggerOperation(
        Summary = "Create a new program",
        Description = "Creates a new program with the provided information. Requires SuperAdmin or Manager role.")]
    [ProducesResponseType(typeof(ApiResult<ProgramsResponseDto>), 201)]
    [ProducesResponseType(typeof(ApiResult<object>), 400)]
    [ProducesResponseType(typeof(ApiResult<object>), 401)]
    [ProducesResponseType(typeof(ApiResult<object>), 403)]
    [ProducesResponseType(typeof(ApiResult<object>), 409)]
    [ProducesResponseType(typeof(ApiResult<object>), 500)]
    public async Task<IActionResult> AddProgram(
        [FromBody, SwaggerParameter("New program data to be created")] CreateProgramRequestDto dto)
    {
        var result = await _programService.AddProgramAsync(dto);

        return CreatedAtAction(
            nameof(GetProgramById),
            new { id = result.Id },
            ApiResult<ProgramsResponseDto>.Success(result, "201", "Program created successfully."));
    }

    // =========================================================================
    // UPDATE  —  PUT /api/programs/{id}      [Admin only]
    // =========================================================================

    [HttpPut("{id:guid}")]
    [Authorize(Roles = "SuperAdmin,Manager")]
    [SwaggerOperation(
        Summary = "Update program information",
        Description = "Updates the details of a specific program by its ID. Requires SuperAdmin or Manager role.")]
    [ProducesResponseType(typeof(ApiResult<ProgramsResponseDto>), 200)]
    [ProducesResponseType(typeof(ApiResult<object>), 400)]
    [ProducesResponseType(typeof(ApiResult<object>), 401)]
    [ProducesResponseType(typeof(ApiResult<object>), 403)]
    [ProducesResponseType(typeof(ApiResult<object>), 404)]
    [ProducesResponseType(typeof(ApiResult<object>), 409)]
    [ProducesResponseType(typeof(ApiResult<object>), 500)]
    public async Task<IActionResult> UpdateProgram(
        [FromRoute] Guid id,
        [FromBody, SwaggerParameter("Updated program data")] UpdateProgramRequestDto dto)
    {
        if (dto == null)
            return BadRequest(ApiResult<object>.Failure("400", "Program update data is required."));

        var result = await _programService.UpdateProgramAsync(id, dto);
        return Ok(ApiResult<ProgramsResponseDto>.Success(result, "200", "Program updated successfully."));
    }

    // =========================================================================
    // DELETE  —  DELETE /api/programs/{id}   [Admin only]
    // =========================================================================

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "SuperAdmin,Manager")]
    [SwaggerOperation(
        Summary = "Delete a program",
        Description = "Soft-deletes a program by its ID. Requires SuperAdmin or Manager role.")]
    [ProducesResponseType(typeof(ApiResult<bool>), 200)]
    [ProducesResponseType(typeof(ApiResult<object>), 401)]
    [ProducesResponseType(typeof(ApiResult<object>), 403)]
    [ProducesResponseType(typeof(ApiResult<object>), 404)]
    [ProducesResponseType(typeof(ApiResult<object>), 500)]
    public async Task<IActionResult> DeleteProgram([FromRoute] Guid id)
    {
        var result = await _programService.DeleteProgramAsync(id);

        if (!result)
            return NotFound(ApiResult<object>.Failure("404", $"Program with ID '{id}' not found."));

        return Ok(ApiResult<bool>.Success(result, "200", "Program deleted successfully."));
    }
}
