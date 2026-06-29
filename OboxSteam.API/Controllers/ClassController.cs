using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OboxSteam.Application.Commons;
using OboxSteam.Application.DTOs.ClassDTO;
using OboxSteam.Application.Interfaces;
using OboxSteam.Application.Utils;
using OboxSteam.Domain.Enums;
using Swashbuckle.AspNetCore.Annotations;

namespace OboxSteam.API.Controllers;

[Route("api/classes")]
[ApiController]
public class ClassController : ControllerBase
{
    private readonly IClassService _classService;

    public ClassController(IClassService classService)
    {
        _classService = classService;
    }

    // =========================================================================
    // GET ALL  —  GET /api/classes
    // =========================================================================

    [HttpGet]
    [SwaggerOperation(
        Summary = "Get all classes",
        Description = "Retrieve a paginated list of cohort classes with basic information only. Use GET /api/classes/{id} for full details including SeatsTaken.")]
    [ProducesResponseType(typeof(ApiResult<Pagination<ClassResponseDto>>), 200)]
    [ProducesResponseType(typeof(ApiResult<object>), 400)]
    public async Task<IActionResult> GetAllClasses(
        [FromQuery, SwaggerParameter(Description = "Search by name or code (optional)")] string? search = null,
        [FromQuery, SwaggerParameter(Description = "Sort by: name, code, startDate, endDate, status, maxCapacity, createdAt")] string? sortBy = null,
        [FromQuery, SwaggerParameter(Description = "Sort in descending order? Default: false")] bool isDescending = false,
        [FromQuery, SwaggerParameter(Description = "Page number, starting from 1")] int page = 1,
        [FromQuery, SwaggerParameter(Description = "Number of items per page")] int pageSize = 10,
        [FromQuery, SwaggerParameter(Description = "Filter by program ID (optional)")] Guid? programId = null,
        [FromQuery, SwaggerParameter(Description = "Filter by class status (optional)")] ClassStatus? status = null,
        [FromQuery, SwaggerParameter(Description = "Filter by mentor ID (optional)")] Guid? mentorId = null)
    {
        if (page < 1 || pageSize < 1)
        {
            return BadRequest(ApiResult<object>.Failure("400", "Invalid pagination parameters."));
        }

        var result = await _classService.GetAllClassesAsync(
            search,
            sortBy,
            isDescending,
            page,
            pageSize,
            programId,
            status,
            mentorId);

        return Ok(ApiResult<Pagination<ClassResponseDto>>.Success(result, "200", "Classes retrieved successfully."));
    }

    // =========================================================================
    // GET WITH STUDENTS  —  GET /api/classes/with-students/{classId}
    // =========================================================================

    [HttpGet("with-students/{classId:guid}")]
    [Authorize(Roles = "Mentor,SuperAdmin,Manager")]
    [SwaggerOperation(
        Summary = "Get class with active student roster",
        Description = "Retrieve class details including active enrolled students. SuperAdmin and Manager may view any class roster. Mentors may only view classes they own. Students and other roles cannot access this endpoint.")]
    [ProducesResponseType(typeof(ApiResult<ClassResponseDto>), 200)]
    [ProducesResponseType(typeof(ApiResult<object>), 401)]
    [ProducesResponseType(typeof(ApiResult<object>), 403)]
    [ProducesResponseType(typeof(ApiResult<object>), 404)]
    public async Task<IActionResult> GetClassWithStudents([FromRoute] Guid classId)
    {
        var result = await _classService.GetClassWithStudentsAsync(classId);

        return Ok(ApiResult<ClassResponseDto>.Success(result, "200", "Class students retrieved successfully."));
    }

    // =========================================================================
    // GET BY ID  —  GET /api/classes/{id}
    // =========================================================================

    [HttpGet("{id:guid}")]
    [SwaggerOperation(
        Summary = "Get class details",
        Description = "Retrieve detailed information for a specific class cohort by its ID, including SeatsTaken.")]
    [ProducesResponseType(typeof(ApiResult<ClassResponseDto>), 200)]
    [ProducesResponseType(typeof(ApiResult<object>), 404)]
    public async Task<IActionResult> GetClassById([FromRoute] Guid id)
    {
        var result = await _classService.GetClassByIdAsync(id);

        return Ok(ApiResult<ClassResponseDto>.Success(result, "200", "Class retrieved successfully."));
    }

    // =========================================================================
    // CREATE  —  POST /api/classes          [Admin only]
    // =========================================================================

    [HttpPost]
    [Authorize(Roles = "SuperAdmin,Manager")]
    [SwaggerOperation(
        Summary = "Create a new class",
        Description = "Creates a new class cohort in Draft status. Requires SuperAdmin or Manager role.")]
    [ProducesResponseType(typeof(ApiResult<ClassResponseDto>), 201)]
    [ProducesResponseType(typeof(ApiResult<object>), 400)]
    [ProducesResponseType(typeof(ApiResult<object>), 401)]
    [ProducesResponseType(typeof(ApiResult<object>), 403)]
    [ProducesResponseType(typeof(ApiResult<object>), 404)]
    [ProducesResponseType(typeof(ApiResult<object>), 409)]
    public async Task<IActionResult> CreateClass(
        [FromBody, SwaggerParameter("New class data to be created")] CreateClassRequestDto dto)
    {
        var result = await _classService.CreateClassAsync(dto);

        return CreatedAtAction(
            nameof(GetClassById),
            new { id = result.Id },
            ApiResult<ClassResponseDto>.Success(result, "201", "Class created successfully."));
    }

    // =========================================================================
    // UPDATE  —  PUT /api/classes/{id}      [Admin only]
    // =========================================================================

    [HttpPut("{id:guid}")]
    [Authorize(Roles = "SuperAdmin,Manager")]
    [SwaggerOperation(
        Summary = "Update class information",
        Description = "Updates class cohort details. Status changes must use Open, Start, or Complete endpoints. Requires SuperAdmin or Manager role.")]
    [ProducesResponseType(typeof(ApiResult<ClassResponseDto>), 200)]
    [ProducesResponseType(typeof(ApiResult<object>), 400)]
    [ProducesResponseType(typeof(ApiResult<object>), 401)]
    [ProducesResponseType(typeof(ApiResult<object>), 403)]
    [ProducesResponseType(typeof(ApiResult<object>), 404)]
    [ProducesResponseType(typeof(ApiResult<object>), 409)]
    public async Task<IActionResult> UpdateClass(
        [FromRoute] Guid id,
        [FromBody, SwaggerParameter("Updated class data")] UpdateClassRequestDto dto)
    {
        if (dto == null)
        {
            return BadRequest(ApiResult<object>.Failure("400", "Class update data is required."));
        }

        var result = await _classService.UpdateClassAsync(id, dto);

        return Ok(ApiResult<ClassResponseDto>.Success(result, "200", "Class updated successfully."));
    }

    // =========================================================================
    // OPEN  —  POST /api/classes/{id}/open   [Admin only]
    // =========================================================================

    [HttpPost("{id:guid}/open")]
    [Authorize(Roles = "SuperAdmin,Manager")]
    [SwaggerOperation(
        Summary = "Open a class for enrollment",
        Description = "Transitions a class from Draft to Open. Requires SuperAdmin or Manager role.")]
    [ProducesResponseType(typeof(ApiResult<ClassResponseDto>), 200)]
    [ProducesResponseType(typeof(ApiResult<object>), 400)]
    [ProducesResponseType(typeof(ApiResult<object>), 401)]
    [ProducesResponseType(typeof(ApiResult<object>), 403)]
    [ProducesResponseType(typeof(ApiResult<object>), 404)]
    public async Task<IActionResult> OpenClass([FromRoute] Guid id)
    {
        var result = await _classService.OpenClassAsync(id);

        return Ok(ApiResult<ClassResponseDto>.Success(result, "200", "Class opened successfully."));
    }

    // =========================================================================
    // START  —  POST /api/classes/{id}/start   [Admin only]
    // =========================================================================

    [HttpPost("{id:guid}/start")]
    [Authorize(Roles = "SuperAdmin,Manager")]
    [SwaggerOperation(
        Summary = "Start a class",
        Description = "Transitions a class from Open to InProgress. Requires SuperAdmin or Manager role.")]
    [ProducesResponseType(typeof(ApiResult<ClassResponseDto>), 200)]
    [ProducesResponseType(typeof(ApiResult<object>), 400)]
    [ProducesResponseType(typeof(ApiResult<object>), 401)]
    [ProducesResponseType(typeof(ApiResult<object>), 403)]
    [ProducesResponseType(typeof(ApiResult<object>), 404)]
    public async Task<IActionResult> StartClass([FromRoute] Guid id)
    {
        var result = await _classService.StartClassAsync(id);

        return Ok(ApiResult<ClassResponseDto>.Success(result, "200", "Class started successfully."));
    }

    // =========================================================================
    // COMPLETE  —  POST /api/classes/{id}/complete   [Admin only]
    // =========================================================================

    [HttpPost("{id:guid}/complete")]
    [Authorize(Roles = "SuperAdmin,Manager")]
    [SwaggerOperation(
        Summary = "Complete a class",
        Description = "Transitions a class from InProgress to Completed. Requires SuperAdmin or Manager role.")]
    [ProducesResponseType(typeof(ApiResult<ClassResponseDto>), 200)]
    [ProducesResponseType(typeof(ApiResult<object>), 400)]
    [ProducesResponseType(typeof(ApiResult<object>), 401)]
    [ProducesResponseType(typeof(ApiResult<object>), 403)]
    [ProducesResponseType(typeof(ApiResult<object>), 404)]
    public async Task<IActionResult> CompleteClass([FromRoute] Guid id)
    {
        var result = await _classService.CompleteClassAsync(id);

        return Ok(ApiResult<ClassResponseDto>.Success(result, "200", "Class completed successfully."));
    }
}
