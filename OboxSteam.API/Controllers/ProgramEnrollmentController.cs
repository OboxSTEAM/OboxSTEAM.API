using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OboxSteam.Application.Commons;
using OboxSteam.Application.DTOs.EnrollmentDTO;
using OboxSteam.Application.Interfaces;
using OboxSteam.Application.Utils;
using Swashbuckle.AspNetCore.Annotations;

namespace OboxSteam.API.Controllers;

[Route("api/program-enrollments")]
[ApiController]
public class ProgramEnrollmentController : ControllerBase
{
    private readonly IProgramEnrollmentService _programEnrollmentService;
    private readonly IEnrollmentCurriculumService _enrollmentCurriculumService;

    public ProgramEnrollmentController(
        IProgramEnrollmentService programEnrollmentService,
        IEnrollmentCurriculumService enrollmentCurriculumService)
    {
        _programEnrollmentService = programEnrollmentService;
        _enrollmentCurriculumService = enrollmentCurriculumService;
    }

    // =========================================================================
    // GET BY ID  —  GET /api/program-enrollments/{id}
    // =========================================================================

    [HttpGet("{id:guid}")]
    [Authorize(Roles = "Student,Parent,SuperAdmin,Manager")]
    [SwaggerOperation(
        Summary = "Get program enrollment by ID",
        Description = "Retrieve a program enrollment. Students see their own; parents see linked students; admins see all.")]
    [ProducesResponseType(typeof(ApiResult<ProgramEnrollmentResponseDto>), 200)]
    [ProducesResponseType(typeof(ApiResult<object>), 401)]
    [ProducesResponseType(typeof(ApiResult<object>), 403)]
    [ProducesResponseType(typeof(ApiResult<object>), 404)]
    public async Task<IActionResult> GetProgramEnrollmentById([FromRoute] Guid id)
    {
        var result = await _programEnrollmentService.GetProgramEnrollmentByIdAsync(id);

        return Ok(ApiResult<ProgramEnrollmentResponseDto>.Success(
            result,
            "200",
            "Program enrollment retrieved successfully."));
    }

    // =========================================================================
    // GET MY / SCOPED LIST  —  GET /api/program-enrollments/me
    // =========================================================================

    [HttpGet("me")]
    [Authorize(Roles = "Student,Parent,SuperAdmin,Manager")]
    [SwaggerOperation(
        Summary = "Get program enrollments for current user",
        Description = "Students: own enrollments. Parents: linked students. Admins: all enrollments.")]
    [ProducesResponseType(typeof(ApiResult<Pagination<ProgramEnrollmentResponseDto>>), 200)]
    [ProducesResponseType(typeof(ApiResult<object>), 400)]
    [ProducesResponseType(typeof(ApiResult<object>), 401)]
    [ProducesResponseType(typeof(ApiResult<object>), 403)]
    public async Task<IActionResult> GetMyProgramEnrollments(
        [FromQuery, SwaggerParameter(Description = "Filter by program ID (optional)")] Guid? programId = null,
        [FromQuery, SwaggerParameter(Description = "Sort by: enrolledAt, progressPercent, status, createdAt")] string? sortBy = null,
        [FromQuery, SwaggerParameter(Description = "Sort in descending order? Default: false")] bool isDescending = false,
        [FromQuery, SwaggerParameter(Description = "Page number, starting from 1")] int page = 1,
        [FromQuery, SwaggerParameter(Description = "Number of items per page")] int pageSize = 10)
    {
        if (page < 1 || pageSize < 1)
        {
            return BadRequest(ApiResult<object>.Failure("400", "Invalid pagination parameters."));
        }

        var result = await _programEnrollmentService.GetMyProgramEnrollmentsAsync(
            programId,
            sortBy,
            isDescending,
            page,
            pageSize);

        return Ok(ApiResult<Pagination<ProgramEnrollmentResponseDto>>.Success(
            result,
            "200",
            "Program enrollments retrieved successfully."));
    }

    // =========================================================================
    // GET CLASS  —  GET /api/program-enrollments/{enrollmentId}/class
    // =========================================================================

    [HttpGet("{enrollmentId:guid}/class")]
    [Authorize(Roles = "Student,Parent,SuperAdmin,Manager")]
    [SwaggerOperation(
        Summary = "Get class for a program enrollment",
        Description = "Returns the active class cohort linked to this program enrollment. "
            + "ClassId is null when the student has not joined a class yet. "
            + "Students see their own; parents see linked students; admins see all.")]
    [ProducesResponseType(typeof(ApiResult<ProgramEnrollmentClassDto>), 200)]
    [ProducesResponseType(typeof(ApiResult<object>), 401)]
    [ProducesResponseType(typeof(ApiResult<object>), 403)]
    [ProducesResponseType(typeof(ApiResult<object>), 404)]
    public async Task<IActionResult> GetProgramEnrollmentClass([FromRoute] Guid enrollmentId)
    {
        var result = await _programEnrollmentService.GetProgramEnrollmentClassAsync(enrollmentId);

        return Ok(ApiResult<ProgramEnrollmentClassDto>.Success(
            result,
            "200",
            "Program enrollment class retrieved successfully."));
    }

    // =========================================================================
    // GET CURRICULUM  —  GET /api/program-enrollments/{enrollmentId}/curriculum
    // =========================================================================

    [HttpGet("{enrollmentId:guid}/curriculum")]
    [Authorize(Roles = "Student,Parent,SuperAdmin,Manager")]
    [SwaggerOperation(
        Summary = "Get enrollment-scoped program curriculum",
        Description = "Returns the curriculum tree with per-student activity status, module locks, and current activity.")]
    [ProducesResponseType(typeof(ApiResult<EnrollmentCurriculumDto>), 200)]
    [ProducesResponseType(typeof(ApiResult<object>), 401)]
    [ProducesResponseType(typeof(ApiResult<object>), 403)]
    [ProducesResponseType(typeof(ApiResult<object>), 404)]
    public async Task<IActionResult> GetEnrollmentCurriculum([FromRoute] Guid enrollmentId)
    {
        var result = await _enrollmentCurriculumService.GetEnrollmentCurriculumAsync(enrollmentId);

        return Ok(ApiResult<EnrollmentCurriculumDto>.Success(
            result,
            "200",
            "Enrollment curriculum retrieved successfully."));
    }

    // =========================================================================
    // CHECKPOINT  —  PATCH /api/program-enrollments/{enrollmentId}/activities/{activityId}/checkpoint
    // =========================================================================

    [HttpPatch("{enrollmentId:guid}/activities/{activityId:guid}/checkpoint")]
    [Authorize(Roles = "Student")]
    [SwaggerOperation(
        Summary = "Save activity learning checkpoint",
        Description = "Persists resume position (video time, PDF page, doc scroll) for an in-progress SelfPaced activity.")]
    [ProducesResponseType(typeof(ApiResult<SaveActivityCheckpointResponseDto>), 200)]
    [ProducesResponseType(typeof(ApiResult<object>), 400)]
    [ProducesResponseType(typeof(ApiResult<object>), 401)]
    [ProducesResponseType(typeof(ApiResult<object>), 403)]
    [ProducesResponseType(typeof(ApiResult<object>), 404)]
    public async Task<IActionResult> SaveActivityCheckpoint(
        [FromRoute] Guid enrollmentId,
        [FromRoute] Guid activityId,
        [FromBody] SaveActivityCheckpointRequestDto request)
    {
        var result = await _enrollmentCurriculumService.SaveActivityCheckpointAsync(
            enrollmentId,
            activityId,
            request);

        return Ok(ApiResult<SaveActivityCheckpointResponseDto>.Success(
            result,
            "200",
            "Activity checkpoint saved successfully."));
    }

    // =========================================================================
    // COMPLETE ACTIVITY  —  POST /api/program-enrollments/{enrollmentId}/activities/{activityId}/complete
    // =========================================================================

    [HttpPost("{enrollmentId:guid}/activities/{activityId:guid}/complete")]
    [Authorize(Roles = "Student")]
    [SwaggerOperation(
        Summary = "Mark activity complete",
        Description = "Marks a SelfPaced activity as done for the enrollment and returns updated progress.")]
    [ProducesResponseType(typeof(ApiResult<CompleteActivityResponseDto>), 200)]
    [ProducesResponseType(typeof(ApiResult<object>), 400)]
    [ProducesResponseType(typeof(ApiResult<object>), 401)]
    [ProducesResponseType(typeof(ApiResult<object>), 403)]
    [ProducesResponseType(typeof(ApiResult<object>), 404)]
    public async Task<IActionResult> CompleteActivity(
        [FromRoute] Guid enrollmentId,
        [FromRoute] Guid activityId,
        [FromBody] CompleteActivityRequestDto? request = null)
    {
        var result = await _enrollmentCurriculumService.CompleteActivityAsync(
            enrollmentId,
            activityId,
            request);

        return Ok(ApiResult<CompleteActivityResponseDto>.Success(
            result,
            "200",
            "Activity marked as complete."));
    }

    // =========================================================================
    // GET BY STUDENT  —  GET /api/program-enrollments/student/{studentId}
    // =========================================================================

    [HttpGet("student/{studentId:guid}")]
    [Authorize(Roles = "Student,Parent,SuperAdmin,Manager")]
    [SwaggerOperation(
        Summary = "Get program enrollments by student ID",
        Description = "List program enrollments for a student. Access rules apply per role (self, linked parent, or admin).")]
    [ProducesResponseType(typeof(ApiResult<Pagination<ProgramEnrollmentResponseDto>>), 200)]
    [ProducesResponseType(typeof(ApiResult<object>), 400)]
    [ProducesResponseType(typeof(ApiResult<object>), 401)]
    [ProducesResponseType(typeof(ApiResult<object>), 403)]
    [ProducesResponseType(typeof(ApiResult<object>), 404)]
    public async Task<IActionResult> GetProgramEnrollmentsByStudentId(
        [FromRoute] Guid studentId,
        [FromQuery, SwaggerParameter(Description = "Sort by: enrolledAt, progressPercent, status, createdAt")] string? sortBy = null,
        [FromQuery, SwaggerParameter(Description = "Sort in descending order? Default: false")] bool isDescending = false,
        [FromQuery, SwaggerParameter(Description = "Page number, starting from 1")] int page = 1,
        [FromQuery, SwaggerParameter(Description = "Number of items per page")] int pageSize = 10)
    {
        if (page < 1 || pageSize < 1)
        {
            return BadRequest(ApiResult<object>.Failure("400", "Invalid pagination parameters."));
        }

        var result = await _programEnrollmentService.GetProgramEnrollmentsByStudentIdAsync(
            studentId,
            sortBy,
            isDescending,
            page,
            pageSize);

        return Ok(ApiResult<Pagination<ProgramEnrollmentResponseDto>>.Success(
            result,
            "200",
            "Program enrollments retrieved successfully."));
    }
}
