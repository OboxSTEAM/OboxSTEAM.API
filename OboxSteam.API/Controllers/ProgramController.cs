using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using OboxSteam.Application.Commons;
using OboxSteam.Application.DTOs.ClassDTO;
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
    private readonly IEnrollmentCurriculumService _enrollmentCurriculumService;
    private readonly IClassService _classService;

    public ProgramController(
        IProgramService programService,
        IEnrollmentCurriculumService enrollmentCurriculumService,
        IClassService classService)
    {
        _programService = programService;
        _enrollmentCurriculumService = enrollmentCurriculumService;
        _classService = classService;
    }

    // =========================================================================
    // GET ALL  —  GET /api/programs
    // =========================================================================

    [HttpGet]
    [SwaggerOperation(
        Summary = "Get all programs",
        Description = "Retrieve a paginated list of program information without modules. Supports search, filter, and sort options.")]
    [ProducesResponseType(typeof(ApiResult<Pagination<ProgramListItemDto>>), 200)]
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
        [FromQuery, SwaggerParameter(Description = "Filter by program status: Draft, Active, Inactive (optional)")] ProgramStatus? status = null,
        [FromQuery, SwaggerParameter(Description = "Filter by category (optional)")] ProgramCategory? category = null)
    {
        if (page < 1 || pageSize < 1)
            return BadRequest(ApiResult<object>.Failure("400", "Invalid pagination parameters."));

        var result = await _programService.GetAllProgramsAsync(
            search, sortBy, isDescending, page, pageSize,
            code, level, rating, skillsGained, status, category);

        return Ok(ApiResult<Pagination<ProgramListItemDto>>.Success(result, "200", "Programs retrieved successfully."));
    }

    // =========================================================================
    // GET ALL WITH MODULES  —  GET /api/programs/with-modules
    // =========================================================================

    [HttpGet("with-modules")]
    [SwaggerOperation(
        Summary = "Get all programs with modules",
        Description = "Retrieve a paginated list of programs including their modules. Supports search, filter, and sort options.")]
    [ProducesResponseType(typeof(ApiResult<Pagination<ProgramsResponseDto>>), 200)]
    [ProducesResponseType(typeof(ApiResult<object>), 400)]
    [ProducesResponseType(typeof(ApiResult<object>), 500)]
    public async Task<IActionResult> GetAllProgramsWithModules(
        [FromQuery, SwaggerParameter(Description = "Search by name or code (optional)")] string? search = null,
        [FromQuery, SwaggerParameter(Description = "Sort by field: name, code, level, rating, price, createdAt (optional)")] string? sortBy = null,
        [FromQuery, SwaggerParameter(Description = "Sort in descending order? Default: false")] bool isDescending = false,
        [FromQuery, SwaggerParameter(Description = "Page number, starting from 1")] int page = 1,
        [FromQuery, SwaggerParameter(Description = "Number of items per page")] int pageSize = 10,
        [FromQuery, SwaggerParameter(Description = "Filter by program code (optional)")] string? code = null,
        [FromQuery, SwaggerParameter(Description = "Filter by difficulty level (optional)")] DifficultyLevel? level = null,
        [FromQuery, SwaggerParameter(Description = "Filter by minimum rating (optional)")] decimal? rating = null,
        [FromQuery, SwaggerParameter(Description = "Filter by skills gained keyword (optional)")] string? skillsGained = null,
        [FromQuery, SwaggerParameter(Description = "Filter by program status: Draft, Active, Inactive (optional)")] ProgramStatus? status = null,
        [FromQuery, SwaggerParameter(Description = "Filter by category (optional)")] ProgramCategory? category = null)
    {
        if (page < 1 || pageSize < 1)
            return BadRequest(ApiResult<object>.Failure("400", "Invalid pagination parameters."));

        var result = await _programService.GetAllProgramsWithModulesAsync(
            search, sortBy, isDescending, page, pageSize,
            code, level, rating, skillsGained, status, category);

        return Ok(ApiResult<Pagination<ProgramsResponseDto>>.Success(result, "200", "Programs with modules retrieved successfully."));
    }

    // =========================================================================
    // GET CURRICULUM  —  GET /api/programs/{id}/curriculum
    // =========================================================================

    [HttpGet("{id:guid}/curriculum")]
    [SwaggerOperation(
        Summary = "Get program curriculum tree",
        Description = "Retrieve a compact curriculum outline for a program: modules, courses or milestones, activities, and materials.")]
    [ProducesResponseType(typeof(ApiResult<ProgramCurriculumDto>), 200)]
    [ProducesResponseType(typeof(ApiResult<object>), 404)]
    [ProducesResponseType(typeof(ApiResult<object>), 500)]
    public async Task<IActionResult> GetProgramCurriculum([FromRoute] Guid id)
    {
        await _enrollmentCurriculumService.EnsureStudentEnrolledInProgramAsync(id);
        var result = await _programService.GetProgramCurriculumAsync(id);
        return Ok(ApiResult<ProgramCurriculumDto>.Success(result, "200", "Program curriculum retrieved successfully."));
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
    // OPEN CLASSES FOR ENROLLMENT  —  GET /api/programs/{id}/open-classes
    // =========================================================================

    [HttpGet("{id:guid}/open-classes")]
    [SwaggerOperation(
        Summary = "List open classes available for enrollment",
        Description = "Public preview of Standard classes that are Open and still have seats, "
            + "including schedule sessions and seat counts. Use before checkout to show recruiting "
            + "cohorts. Pass classId when starting checkout to soft-hold a seat for 5 minutes. "
            + "Checkout is blocked when the selected class has no capacity.")]
    [ProducesResponseType(typeof(ApiResult<IReadOnlyList<OpenEnrollmentClassDto>>), 200)]
    [ProducesResponseType(typeof(ApiResult<object>), 400)]
    [ProducesResponseType(typeof(ApiResult<object>), 404)]
    public async Task<IActionResult> GetOpenEnrollmentClasses(
        [FromRoute] Guid id,
        [FromQuery, SwaggerParameter(
            Description = "Optional class the learner viewed before pay — soft-sorted first when still enrollable")]
        Guid? preferredClassId = null)
    {
        var result = await _classService.GetOpenEnrollmentClassesAsync(id, preferredClassId);
        return Ok(ApiResult<IReadOnlyList<OpenEnrollmentClassDto>>.Success(
            result,
            "200",
            "Open enrollment classes retrieved successfully."));
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
    [Authorize(Roles = "Admin,Manager")]
    [Consumes("multipart/form-data")]
    [SwaggerOperation(
        Summary = "Create a new program",
        Description = "Creates a new program with form-data program information and uploads thumbnail image. Requires Admin or Manager role.")]
    [ProducesResponseType(typeof(ApiResult<ProgramsResponseDto>), 201)]
    [ProducesResponseType(typeof(ApiResult<object>), 400)]
    [ProducesResponseType(typeof(ApiResult<object>), 401)]
    [ProducesResponseType(typeof(ApiResult<object>), 403)]
    [ProducesResponseType(typeof(ApiResult<object>), 409)]
    [ProducesResponseType(typeof(ApiResult<object>), 500)]
    public async Task<IActionResult> AddProgram(
        [FromForm, SwaggerParameter("Program data to be created (multipart field prefix: data.<PropertyName>)")] CreateProgramRequestDto data,
        IFormFile file)
    {
        var result = await _programService.CreateProgramAsync(data, file);

        return CreatedAtAction(
            nameof(GetProgramById),
            new { id = result.Id },
            ApiResult<ProgramsResponseDto>.Success(result, "201", "Program created successfully."));
    }

    // =========================================================================
    // UPDATE  —  PUT /api/programs/{id}      [Admin only]
    // =========================================================================

    [HttpPut("{id:guid}")]
    [Authorize(Roles = "Admin,Manager")]
    [SwaggerOperation(
        Summary = "Update program information",
        Description = "Updates the details of a specific program by its ID. Requires Admin or Manager role.")]
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

    /// <summary>
    /// Upload thumbnail for a specific program.
    /// </summary>
    /// <param name="id">Program ID.</param>
    /// <param name="file">Image file (jpg, jpeg, png, webp). Max 5 MB.</param>
    /// <returns>Updated program with new thumbnail URL.</returns>
    [HttpPost("{id:guid}/thumbnail")]
    [Authorize(Roles = "Admin,Manager")]
    [SwaggerOperation(
        Summary = "Upload program thumbnail",
        Description = "Uploads a new thumbnail image for the specified program. Replaces the existing thumbnail if one exists.")]
    [ProducesResponseType(typeof(ApiResult<ProgramsResponseDto>), 200)]
    [ProducesResponseType(typeof(ApiResult<object>), 400)]
    [ProducesResponseType(typeof(ApiResult<object>), 401)]
    [ProducesResponseType(typeof(ApiResult<object>), 403)]
    [ProducesResponseType(typeof(ApiResult<object>), 404)]
    public async Task<IActionResult> UploadProgramThumbnail([FromRoute] Guid id, IFormFile file)
    {
        var result = await _programService.UploadProgramThumbnailAsync(id, file);
        return Ok(ApiResult<ProgramsResponseDto>.Success(result, "200", "Program thumbnail uploaded successfully."));
    }

    // =========================================================================
    // DELETE  —  DELETE /api/programs/{id}   [Admin only]
    // =========================================================================

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "Admin,Manager")]
    [SwaggerOperation(
        Summary = "Delete a program",
        Description = "Soft-deletes a program by its ID. Requires Admin or Manager role.")]
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
