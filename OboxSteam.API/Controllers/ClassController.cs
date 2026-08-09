using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OboxSteam.Application.Commons;
using OboxSteam.Application.DTOs.ClassDTO;
using OboxSteam.Application.DTOs.ClassMentorRequestDTO;
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
    private readonly IClassMentorRequestService _classMentorRequestService;

    public ClassController(
        IClassService classService,
        IClassMentorRequestService classMentorRequestService)
    {
        _classService = classService;
        _classMentorRequestService = classMentorRequestService;
    }

    // =========================================================================
    // MENTOR BOARD  —  GET /api/classes/mentor-board
    // =========================================================================

    [HttpGet("mentor-board")]
    [Authorize(Roles = "Mentor")]
    [SwaggerOperation(
        Summary = "Mentor board of available classes",
        Description = "Lists Draft/Open classes with no assigned mentor. Prefer GET /api/class-mentor-requests/board.")]
    [ProducesResponseType(typeof(ApiResult<Pagination<ClassMentorBoardItemDto>>), 200)]
    [ProducesResponseType(typeof(ApiResult<object>), 401)]
    [ProducesResponseType(typeof(ApiResult<object>), 403)]
    public async Task<IActionResult> GetMentorBoard(
        [FromQuery, SwaggerParameter(Description = "Search by name or code (optional)")] string? search = null,
        [FromQuery, SwaggerParameter(Description = "Sort by: name, code, startDate")] string? sortBy = null,
        [FromQuery] bool isDescending = false,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] Guid? programId = null,
        [FromQuery, SwaggerParameter(Description = "When true, only classes that share at least one RequiredSkill with the mentor. Default: false (show all).")] bool matchMySkills = false)
    {
        if (page < 1 || pageSize < 1)
        {
            return BadRequest(ApiResult<object>.Failure("400", "Invalid pagination parameters."));
        }

        var result = await _classMentorRequestService.GetMentorBoardAsync(
            search, sortBy, isDescending, page, pageSize, programId, matchMySkills);

        return Ok(ApiResult<Pagination<ClassMentorBoardItemDto>>.Success(
            result, "200", "Mentor board retrieved successfully."));
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
    [Authorize(Roles = "Student,Mentor,Admin,Manager")]
    [SwaggerOperation(
        Summary = "Get class with active student roster",
        Description = "Retrieve class details including active enrolled students. Students may view the roster only for a class they are actively enrolled in. Admin and Manager may view any class roster. Mentors may only view classes they own.")]
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
    // GET WITH SESSIONS  —  GET /api/classes/with-sessions/{classId}
    // =========================================================================

    [HttpGet("with-sessions/{classId:guid}")]
    [SwaggerOperation(
        Summary = "Get class with scheduled sessions",
        Description = "Retrieve class details including all scheduled sessions for the cohort, ordered by start time.")]
    [ProducesResponseType(typeof(ApiResult<ClassWithSessionsResponseDto>), 200)]
    [ProducesResponseType(typeof(ApiResult<object>), 404)]
    public async Task<IActionResult> GetClassWithSessions([FromRoute] Guid classId)
    {
        var result = await _classService.GetClassWithSessionsAsync(classId);

        return Ok(ApiResult<ClassWithSessionsResponseDto>.Success(result, "200", "Class sessions retrieved successfully."));
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
    [Authorize(Roles = "Admin,Manager")]
    [SwaggerOperation(
        Summary = "Create a new class",
        Description = "Creates a new class cohort in Draft status. Requires Admin or Manager role.")]
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
    [Authorize(Roles = "Admin,Manager")]
    [SwaggerOperation(
        Summary = "Update class information",
        Description = "Updates class cohort details. Status changes must use Open, Start, or Complete endpoints. Requires Admin or Manager role.")]
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
    [Authorize(Roles = "Admin,Manager")]
    [SwaggerOperation(
        Summary = "Open a class for enrollment",
        Description = "Transitions a class from Draft to Open. Requires Admin or Manager role.")]
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
    [Authorize(Roles = "Admin,Manager")]
    [SwaggerOperation(
        Summary = "Start a class",
        Description = "Transitions a class from Open to InProgress. Requires Admin or Manager role.")]
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
    [Authorize(Roles = "Admin,Manager")]
    [SwaggerOperation(
        Summary = "Complete a class",
        Description = "Transitions a class from InProgress to Completed. Requires Admin or Manager role.")]
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

    // =========================================================================
    // DELETE  —  DELETE /api/classes/{id}   [Manager only]
    // =========================================================================

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "Manager")]
    [SwaggerOperation(
        Summary = "Delete a class",
        Description = "Soft-deletes a Draft or Open class cohort and its sessions. Open classes may only be deleted when they have no active students. Requires Manager role.")]
    [ProducesResponseType(typeof(ApiResult<bool>), 200)]
    [ProducesResponseType(typeof(ApiResult<object>), 400)]
    [ProducesResponseType(typeof(ApiResult<object>), 401)]
    [ProducesResponseType(typeof(ApiResult<object>), 403)]
    [ProducesResponseType(typeof(ApiResult<object>), 404)]
    [ProducesResponseType(typeof(ApiResult<object>), 409)]
    public async Task<IActionResult> DeleteClass([FromRoute] Guid id)
    {
        await _classService.DeleteClassAsync(id);

        return Ok(ApiResult<bool>.Success(true, "200", "Class deleted successfully."));
    }
}
