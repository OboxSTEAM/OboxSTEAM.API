using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OboxSteam.Application.Commons;
using OboxSteam.Application.DTOs.ExpertDTO;
using OboxSteam.Application.Interfaces;
using OboxSteam.Application.Utils;
using Swashbuckle.AspNetCore.Annotations;

namespace OboxSteam.API.Controllers;

[Route("api/experts")]
[ApiController]
public class ExpertController : ControllerBase
{
    private readonly IExpertService _expertService;

    public ExpertController(IExpertService expertService)
    {
        _expertService = expertService;
    }

    // =========================================================================
    // GET ALL  —  GET /api/experts
    // =========================================================================

    [HttpGet]
    [SwaggerOperation(
        Summary = "Get all experts",
        Description = "Retrieve a paginated list of experts with optional search, filter, and sort options.")]
    [ProducesResponseType(typeof(ApiResult<Pagination<ExpertResponseDto>>), 200)]
    [ProducesResponseType(typeof(ApiResult<object>), 400)]
    public async Task<IActionResult> GetAllExperts(
        [FromQuery, SwaggerParameter(Description = "Search by name or code (optional)")] string? search = null,
        [FromQuery, SwaggerParameter(Description = "Sort by field: fullName, code, createdAt (optional)")] string? sortBy = null,
        [FromQuery, SwaggerParameter(Description = "Sort in descending order? Default: false")] bool isDescending = false,
        [FromQuery, SwaggerParameter(Description = "Page number, starting from 1")] int page = 1,
        [FromQuery, SwaggerParameter(Description = "Number of items per page")] int pageSize = 10,
        [FromQuery, SwaggerParameter(Description = "Filter by expert code (optional)")] string? code = null)
    {
        if (page < 1 || pageSize < 1)
        {
            return BadRequest(ApiResult<object>.Failure("400", "Invalid pagination parameters."));
        }

        var result = await _expertService.GetAllExpertsAsync(search, sortBy, isDescending, page, pageSize, code);

        return Ok(ApiResult<Pagination<ExpertResponseDto>>.Success(result, "200", "Experts retrieved successfully."));
    }

    // =========================================================================
    // GET BY ID  —  GET /api/experts/{id}
    // =========================================================================

    [HttpGet("{id:guid}")]
    [SwaggerOperation(
        Summary = "Get expert details",
        Description = "Retrieve detailed information for a specific expert by its ID.")]
    [ProducesResponseType(typeof(ApiResult<ExpertResponseDto>), 200)]
    [ProducesResponseType(typeof(ApiResult<object>), 404)]
    public async Task<IActionResult> GetExpertById([FromRoute] Guid id)
    {
        var result = await _expertService.GetExpertByIdAsync(id);
        return Ok(ApiResult<ExpertResponseDto>.Success(result, "200", "Expert retrieved successfully."));
    }

    // =========================================================================
    // CREATE  —  POST /api/experts          [Admin only]
    // =========================================================================

    [HttpPost]
    [Authorize(Roles = "SuperAdmin,Manager")]
    [SwaggerOperation(
        Summary = "Create a new expert",
        Description = "Creates a new expert with the provided information. Requires SuperAdmin or Manager role.")]
    [ProducesResponseType(typeof(ApiResult<ExpertResponseDto>), 201)]
    [ProducesResponseType(typeof(ApiResult<object>), 400)]
    [ProducesResponseType(typeof(ApiResult<object>), 401)]
    [ProducesResponseType(typeof(ApiResult<object>), 403)]
    [ProducesResponseType(typeof(ApiResult<object>), 409)]
    public async Task<IActionResult> AddExpert(
        [FromBody, SwaggerParameter("New expert data to be created")] ExpertCreateDto dto)
    {
        var result = await _expertService.AddExpertAsync(dto);

        return CreatedAtAction(
            nameof(GetExpertById),
            new { id = result.Id },
            ApiResult<ExpertResponseDto>.Success(result, "201", "Expert created successfully."));
    }

    // =========================================================================
    // ADD PROGRAM  —  POST /api/experts/{expertId}/programs/{programId}
    // =========================================================================

    [HttpPost("{expertId:guid}/programs/{programId:guid}")]
    [Authorize(Roles = "SuperAdmin,Manager")]
    [SwaggerOperation(
        Summary = "Add program to expert",
        Description = "Assigns a program to an expert. Optional request body may include RoleInBoard. Requires SuperAdmin or Manager role.")]
    [ProducesResponseType(typeof(ApiResult<ExpertProgramSummaryDto>), 201)]
    [ProducesResponseType(typeof(ApiResult<object>), 400)]
    [ProducesResponseType(typeof(ApiResult<object>), 401)]
    [ProducesResponseType(typeof(ApiResult<object>), 403)]
    [ProducesResponseType(typeof(ApiResult<object>), 404)]
    [ProducesResponseType(typeof(ApiResult<object>), 409)]
    public async Task<IActionResult> AddProgramToExpert(
        [FromRoute] Guid expertId,
        [FromRoute] Guid programId,
        [FromBody, SwaggerParameter("Optional role in program board")] AddProgramToExpertDto? dto = null)
    {
        var result = await _expertService.AddProgramToExpertAsync(expertId, programId, dto);

        return CreatedAtAction(
            nameof(GetExpertById),
            new { id = expertId },
            ApiResult<ExpertProgramSummaryDto>.Success(result, "201", "Program assigned to expert successfully."));
    }

    // =========================================================================
    // UPDATE  —  PUT /api/experts/{id}      [Admin only]
    // =========================================================================

    [HttpPut("{id:guid}")]
    [Authorize(Roles = "SuperAdmin,Manager")]
    [SwaggerOperation(
        Summary = "Update expert information",
        Description = "Updates the details of a specific expert by its ID. Requires SuperAdmin or Manager role.")]
    [ProducesResponseType(typeof(ApiResult<ExpertResponseDto>), 200)]
    [ProducesResponseType(typeof(ApiResult<object>), 400)]
    [ProducesResponseType(typeof(ApiResult<object>), 401)]
    [ProducesResponseType(typeof(ApiResult<object>), 403)]
    [ProducesResponseType(typeof(ApiResult<object>), 404)]
    [ProducesResponseType(typeof(ApiResult<object>), 409)]
    public async Task<IActionResult> UpdateExpert(
        [FromRoute] Guid id,
        [FromBody, SwaggerParameter("Updated expert data")] ExpertUpdateDto dto)
    {
        if (dto == null)
        {
            return BadRequest(ApiResult<object>.Failure("400", "Expert update data is required."));
        }

        var result = await _expertService.UpdateExpertAsync(id, dto);
        return Ok(ApiResult<ExpertResponseDto>.Success(result, "200", "Expert updated successfully."));
    }

    // =========================================================================
    // UPDATE PROGRAM  —  PUT /api/experts/{expertId}/programs/{programId}
    // =========================================================================

    [HttpPut("{expertId:guid}/programs/{programId:guid}")]
    [Authorize(Roles = "SuperAdmin,Manager")]
    [SwaggerOperation(
        Summary = "Update expert program assignment",
        Description = "Updates the program assignment for an expert. Requires SuperAdmin or Manager role.")]
    [ProducesResponseType(typeof(ApiResult<ExpertProgramSummaryDto>), 200)]
    [ProducesResponseType(typeof(ApiResult<object>), 400)]
    [ProducesResponseType(typeof(ApiResult<object>), 401)]
    [ProducesResponseType(typeof(ApiResult<object>), 403)]
    [ProducesResponseType(typeof(ApiResult<object>), 404)]
    public async Task<IActionResult> UpdateProgramOfExpert([FromRoute] Guid expertId, [FromRoute] Guid programId)
    {
        var result = await _expertService.UpdateProgramOfExpertAsync(expertId, programId);
        return Ok(ApiResult<ExpertProgramSummaryDto>.Success(result, "200", "Expert program updated successfully."));
    }

    // =========================================================================
    // DELETE  —  DELETE /api/experts/{id}   [Admin only]
    // =========================================================================

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "SuperAdmin,Manager")]
    [SwaggerOperation(
        Summary = "Delete an expert",
        Description = "Soft-deletes an expert by its ID. Requires SuperAdmin or Manager role.")]
    [ProducesResponseType(typeof(ApiResult<bool>), 200)]
    [ProducesResponseType(typeof(ApiResult<object>), 401)]
    [ProducesResponseType(typeof(ApiResult<object>), 403)]
    [ProducesResponseType(typeof(ApiResult<object>), 404)]
    public async Task<IActionResult> DeleteExpert([FromRoute] Guid id)
    {
        var result = await _expertService.DeleteExpertAsync(id);

        if (!result)
        {
            return NotFound(ApiResult<object>.Failure("404", $"Expert with ID '{id}' not found."));
        }

        return Ok(ApiResult<bool>.Success(result, "200", "Expert deleted successfully."));
    }

    // =========================================================================
    // REMOVE PROGRAM  —  DELETE /api/experts/{expertId}/programs/{programId}
    // =========================================================================

    [HttpDelete("{expertId:guid}/programs/{programId:guid}")]
    [Authorize(Roles = "SuperAdmin,Manager")]
    [SwaggerOperation(
        Summary = "Remove program from expert",
        Description = "Removes a program assignment from an expert. Requires SuperAdmin or Manager role.")]
    [ProducesResponseType(typeof(ApiResult<bool>), 200)]
    [ProducesResponseType(typeof(ApiResult<object>), 401)]
    [ProducesResponseType(typeof(ApiResult<object>), 403)]
    [ProducesResponseType(typeof(ApiResult<object>), 404)]
    public async Task<IActionResult> RemoveProgramFromExpert([FromRoute] Guid expertId, [FromRoute] Guid programId)
    {
        var result = await _expertService.RemoveProgramFromExpertAsync(expertId, programId);
        return Ok(ApiResult<bool>.Success(result, "200", "Program removed from expert successfully."));
    }
}
