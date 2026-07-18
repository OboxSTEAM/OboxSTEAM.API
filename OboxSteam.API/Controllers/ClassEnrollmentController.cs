using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OboxSteam.Application.Commons;
using OboxSteam.Application.DTOs.ClassEnrollmentDTO;
using OboxSteam.Application.Interfaces;
using OboxSteam.Application.Utils;
using Swashbuckle.AspNetCore.Annotations;

namespace OboxSteam.API.Controllers;

[Route("api/class-enrollments")]
[ApiController]
public class ClassEnrollmentController : ControllerBase
{
    private readonly IClassEnrollmentService _classEnrollmentService;

    public ClassEnrollmentController(IClassEnrollmentService classEnrollmentService)
    {
        _classEnrollmentService = classEnrollmentService;
    }

    // =========================================================================
    // ENROLL  —  POST /api/class-enrollments          [Student only]
    // =========================================================================

    [HttpPost]
    [Authorize(Roles = "Student")]
    [SwaggerOperation(
        Summary = "Enroll in a class",
        Description = "Joins a class cohort within an active program enrollment. The selected class must belong to the same program. Requires Student role.")]
    [ProducesResponseType(typeof(ApiResult<ClassEnrollmentResponseDto>), 201)]
    [ProducesResponseType(typeof(ApiResult<object>), 400)]
    [ProducesResponseType(typeof(ApiResult<object>), 401)]
    [ProducesResponseType(typeof(ApiResult<object>), 403)]
    [ProducesResponseType(typeof(ApiResult<object>), 404)]
    [ProducesResponseType(typeof(ApiResult<object>), 409)]
    public async Task<IActionResult> EnrollClass(
        [FromBody, SwaggerParameter("Class enrollment request")] CreateClassEnrollmentRequestDto dto)
    {
        var result = await _classEnrollmentService.EnrollClassAsync(dto);

        return CreatedAtAction(
            nameof(GetClassEnrollmentById),
            new { id = result.Id },
            ApiResult<ClassEnrollmentResponseDto>.Success(result, "201", "Class enrollment created successfully."));
    }

    // =========================================================================
    // TRANSFER  —  PUT /api/class-enrollments/{id}   [Student only]
    // =========================================================================

    [HttpPut("{id:guid}")]
    [Authorize(Roles = "Student")]
    [SwaggerOperation(
        Summary = "Transfer to another class",
        Description = "Moves the student from the current class to another cohort within the same program. Requires Student role.")]
    [ProducesResponseType(typeof(ApiResult<ClassEnrollmentResponseDto>), 200)]
    [ProducesResponseType(typeof(ApiResult<object>), 400)]
    [ProducesResponseType(typeof(ApiResult<object>), 401)]
    [ProducesResponseType(typeof(ApiResult<object>), 403)]
    [ProducesResponseType(typeof(ApiResult<object>), 404)]
    [ProducesResponseType(typeof(ApiResult<object>), 409)]
    public async Task<IActionResult> TransferClass(
        [FromRoute] Guid id,
        [FromBody, SwaggerParameter("Class transfer request")] UpdateClassEnrollmentRequestDto dto)
    {
        if (dto == null)
        {
            return BadRequest(ApiResult<object>.Failure("400", "Class transfer data is required."));
        }

        var result = await _classEnrollmentService.TransferClassAsync(id, dto);

        return Ok(ApiResult<ClassEnrollmentResponseDto>.Success(result, "200", "Class transfer completed successfully."));
    }

    // =========================================================================
    // MANAGER TRANSFER  —  PUT /api/class-enrollments/manager-transfer/{id}   [Manager only]
    // =========================================================================

    [HttpPut("manager-transfer/{id:guid}")]
    [Authorize(Roles = "Manager")]
    [SwaggerOperation(
        Summary = "Transfer a student to another class (Manager)",
        Description = "Marks the student's current active class enrollment as Transferred and creates a new Active enrollment in another Open cohort within the same program. Target class must be Open (not yet started). Requires Manager role.")]
    [ProducesResponseType(typeof(ApiResult<ClassEnrollmentResponseDto>), 200)]
    [ProducesResponseType(typeof(ApiResult<object>), 400)]
    [ProducesResponseType(typeof(ApiResult<object>), 401)]
    [ProducesResponseType(typeof(ApiResult<object>), 403)]
    [ProducesResponseType(typeof(ApiResult<object>), 404)]
    [ProducesResponseType(typeof(ApiResult<object>), 409)]
    public async Task<IActionResult> TransferClassByManager(
        [FromRoute] Guid id,
        [FromBody, SwaggerParameter("Manager class transfer request")] ManagerTransferClassRequestDto dto)
    {
        if (dto == null)
        {
            return BadRequest(ApiResult<object>.Failure("400", "Class transfer data is required."));
        }

        var result = await _classEnrollmentService.TransferClassByManagerAsync(id, dto);

        return Ok(ApiResult<ClassEnrollmentResponseDto>.Success(result, "200", "Class transfer completed successfully."));
    }

    // =========================================================================
    // GET BY ID  —  GET /api/class-enrollments/{id}
    // =========================================================================

    [HttpGet("{id:guid}")]
    [Authorize(Roles = "Student,Parent,SuperAdmin,Manager")]
    [SwaggerOperation(
        Summary = "Get class enrollment by ID",
        Description = "Retrieve a class enrollment. Students see their own; parents see linked students; admins see all.")]
    [ProducesResponseType(typeof(ApiResult<ClassEnrollmentResponseDto>), 200)]
    [ProducesResponseType(typeof(ApiResult<object>), 401)]
    [ProducesResponseType(typeof(ApiResult<object>), 403)]
    [ProducesResponseType(typeof(ApiResult<object>), 404)]
    public async Task<IActionResult> GetClassEnrollmentById([FromRoute] Guid id)
    {
        var result = await _classEnrollmentService.GetClassEnrollmentByIdAsync(id);

        return Ok(ApiResult<ClassEnrollmentResponseDto>.Success(
            result,
            "200",
            "Class enrollment retrieved successfully."));
    }

    // =========================================================================
    // GET BY PROGRAM ENROLLMENT  —  GET /api/class-enrollments/program-enrollment/{programEnrollmentId}
    // =========================================================================

    [HttpGet("program-enrollment/{programEnrollmentId:guid}")]
    [Authorize(Roles = "Student,Parent,SuperAdmin,Manager")]
    [SwaggerOperation(
        Summary = "Get class enrollments by program enrollment",
        Description = "Lists class enrollments for a program enrollment. Access is enforced per role.")]
    [ProducesResponseType(typeof(ApiResult<Pagination<ClassEnrollmentResponseDto>>), 200)]
    [ProducesResponseType(typeof(ApiResult<object>), 400)]
    [ProducesResponseType(typeof(ApiResult<object>), 401)]
    [ProducesResponseType(typeof(ApiResult<object>), 403)]
    [ProducesResponseType(typeof(ApiResult<object>), 404)]
    public async Task<IActionResult> GetClassEnrollmentsByProgramEnrollment(
        [FromRoute] Guid programEnrollmentId,
        [FromQuery, SwaggerParameter(Description = "Sort by: status, enrolledAt, createdAt, className, classCode")] string? sortBy = null,
        [FromQuery, SwaggerParameter(Description = "Sort in descending order? Default: false")] bool isDescending = false,
        [FromQuery, SwaggerParameter(Description = "Page number, starting from 1")] int page = 1,
        [FromQuery, SwaggerParameter(Description = "Number of items per page")] int pageSize = 10)
    {
        if (page < 1 || pageSize < 1)
        {
            return BadRequest(ApiResult<object>.Failure("400", "Invalid pagination parameters."));
        }

        var result = await _classEnrollmentService.GetClassEnrollmentsByProgramEnrollmentAsync(
            programEnrollmentId,
            sortBy,
            isDescending,
            page,
            pageSize);

        return Ok(ApiResult<Pagination<ClassEnrollmentResponseDto>>.Success(
            result,
            "200",
            "Class enrollments retrieved successfully."));
    }
}
