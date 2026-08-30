using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OboxSteam.Application.Commons;
using OboxSteam.Application.DTOs.AssignmentDTO;
using OboxSteam.Application.DTOs.AssignmentSubmissionDTO;
using OboxSteam.Application.Interfaces;
using OboxSteam.Application.Utils;
using OboxSteam.Domain.Enums;
using Swashbuckle.AspNetCore.Annotations;

namespace OboxSteam.API.Controllers;

[Route("api/assignments")]
[ApiController]
public class AssignmentController : ControllerBase
{
    private readonly IAssignmentService _assignmentService;

    public AssignmentController(IAssignmentService assignmentService)
    {
        _assignmentService = assignmentService;
    }

    // =========================================================================
    // GET ALL  —  GET /api/assignments
    // =========================================================================

    /// <summary>
    /// Get a paginated list of assignments with module/program context.
    /// </summary>
    [HttpGet]
    [SwaggerOperation(
        Summary = "Get all assignments",
        Description = "Retrieve a paginated list of assignments, each carrying module/program " +
                      "context for the Edit deep-link. Supports search, filter, and sort options.")]
    [ProducesResponseType(typeof(ApiResult<Pagination<AssignmentListItemDto>>), 200)]
    [ProducesResponseType(typeof(ApiResult<object>), 400)]
    [ProducesResponseType(typeof(ApiResult<object>), 500)]
    public async Task<IActionResult> GetAllAssignments(
        [FromQuery, SwaggerParameter(Description = "Search by title, code, module, or program name (optional)")] string? search = null,
        [FromQuery, SwaggerParameter(Description = "Sort by field: title, code, createdAt, dueDate, assignmentType, moduleName, programName (optional)")] string? sortBy = null,
        [FromQuery, SwaggerParameter(Description = "Sort in descending order? Default: true")] bool isDescending = true,
        [FromQuery, SwaggerParameter(Description = "Page number, starting from 1")] int page = 1,
        [FromQuery, SwaggerParameter(Description = "Number of items per page")] int pageSize = 10,
        [FromQuery, SwaggerParameter(Description = "Filter by module (optional)")] Guid? moduleId = null,
        [FromQuery, SwaggerParameter(Description = "Filter by program (optional)")] Guid? programId = null,
        [FromQuery, SwaggerParameter(Description = "Filter by course (optional)")] Guid? courseId = null,
        [FromQuery, SwaggerParameter(Description = "Filter by assignment type: Quiz, Retrospective, FileUpload (optional)")] AssignmentType? assignmentType = null)
    {
        if (page < 1 || pageSize < 1)
            return BadRequest(ApiResult<object>.Failure("400", "Invalid pagination parameters."));

        var result = await _assignmentService.GetAllAssignments(
            search, sortBy, isDescending, page, pageSize,
            moduleId, programId, courseId, assignmentType);

        return Ok(ApiResult<Pagination<AssignmentListItemDto>>.Success(
            result, "200", "Assignments retrieved successfully."));
    }

    [HttpGet("{assignmentId:guid}")]
    [SwaggerOperation(
        Summary = "Get assignment by ID",
        Description = "Retrieve an assignment by its ID. Students must have a module enrollment "
            + "(any status, including Completed) in the assignment's module.")]
    [ProducesResponseType(typeof(ApiResult<AssignmentResponseDto>), 200)]
    [ProducesResponseType(typeof(ApiResult<object>), 403)]
    [ProducesResponseType(typeof(ApiResult<object>), 404)]
    public async Task<IActionResult> GetAssignmentById(Guid assignmentId)
    {
        var result = await _assignmentService.GetAssignmentById(assignmentId);
        if (result == null)
            return NotFound(ApiResult<object>.Failure("404", "Assignment not found."));

        return Ok(ApiResult<AssignmentResponseDto>.Success(result, "200", "Assignment retrieved successfully."));
    }

    [HttpGet("{assignmentId:guid}/submissions")]
    [Authorize(Roles = "Mentor,Manager,Admin")]
    [SwaggerOperation(
        Summary = "List assignment submissions for a class",
        Description = "Returns submissions of the assignment made by students actively enrolled in the given class. Mentors may only view classes they are assigned to.")]
    [ProducesResponseType(typeof(ApiResult<List<AssignmentSubmissionListItemDto>>), 200)]
    [ProducesResponseType(typeof(ApiResult<object>), 400)]
    [ProducesResponseType(typeof(ApiResult<object>), 401)]
    [ProducesResponseType(typeof(ApiResult<object>), 403)]
    [ProducesResponseType(typeof(ApiResult<object>), 404)]
    public async Task<IActionResult> GetAssignmentSubmissions(
        Guid assignmentId,
        [FromQuery, SwaggerParameter(Description = "Class (cohort) whose students' submissions are listed")] Guid classId)
    {
        var result = await _assignmentService.GetAssignmentSubmissions(assignmentId, classId);

        return Ok(ApiResult<List<AssignmentSubmissionListItemDto>>.Success(
            result, "200", "Assignment submissions retrieved successfully."));
    }

    [HttpPost]
    [Authorize(Roles = "Admin,Manager")]
    [SwaggerOperation(
        Summary = "Create an assignment",
        Description = "Creates a new assignment for a module. Requires Admin or Manager role.")]
    [ProducesResponseType(typeof(ApiResult<AssignmentResponseDto>), 201)]
    [ProducesResponseType(typeof(ApiResult<object>), 400)]
    [ProducesResponseType(typeof(ApiResult<object>), 401)]
    [ProducesResponseType(typeof(ApiResult<object>), 403)]
    [ProducesResponseType(typeof(ApiResult<object>), 404)]
    [ProducesResponseType(typeof(ApiResult<object>), 409)]
    public async Task<IActionResult> CreateAssignment(
        [FromBody, SwaggerParameter("New assignment data")] CreateAssignmentRequestDto request)
    {
        var result = await _assignmentService.CreateAssignment(request);

        return CreatedAtAction(
            nameof(GetAssignmentById),
            new { assignmentId = result.Id },
            ApiResult<AssignmentResponseDto>.Success(result, "201", "Assignment created successfully."));
    }

    [HttpPut("{assignmentId:guid}")]
    [Authorize(Roles = "Admin,Manager,Mentor")]
    [SwaggerOperation(
        Summary = "Update an assignment",
        Description = "Updates an assignment by its ID. Managers/Admins may update all catalog fields. Mentors may update Title and Description for assignments in programs they teach. Assignment windows are set on the class session.")]
    [ProducesResponseType(typeof(ApiResult<AssignmentResponseDto>), 200)]
    [ProducesResponseType(typeof(ApiResult<object>), 400)]
    [ProducesResponseType(typeof(ApiResult<object>), 401)]
    [ProducesResponseType(typeof(ApiResult<object>), 403)]
    [ProducesResponseType(typeof(ApiResult<object>), 404)]
    [ProducesResponseType(typeof(ApiResult<object>), 409)]
    public async Task<IActionResult> UpdateAssignment(
        Guid assignmentId,
        [FromBody, SwaggerParameter("Updated assignment data")] UpdateAssignmentRequestDto request)
    {
        if (request == null)
            return BadRequest(ApiResult<object>.Failure("400", "Assignment update data is required."));

        var result = await _assignmentService.UpdateAssignment(assignmentId, request);
        if (result == null)
            return NotFound(ApiResult<object>.Failure("404", "Assignment not found."));

        return Ok(ApiResult<AssignmentResponseDto>.Success(result, "200", "Assignment updated successfully."));
    }

    [HttpDelete("{assignmentId:guid}")]
    [Authorize(Roles = "Admin,Manager")]
    [SwaggerOperation(
        Summary = "Delete an assignment",
        Description = "Soft-deletes an assignment by its ID. Requires Admin or Manager role.")]
    [ProducesResponseType(typeof(ApiResult<bool>), 200)]
    [ProducesResponseType(typeof(ApiResult<object>), 401)]
    [ProducesResponseType(typeof(ApiResult<object>), 403)]
    [ProducesResponseType(typeof(ApiResult<object>), 404)]
    [ProducesResponseType(typeof(ApiResult<object>), 409)]
    public async Task<IActionResult> DeleteAssignment(Guid assignmentId)
    {
        var result = await _assignmentService.DeleteAssignment(assignmentId);
        if (!result)
            return NotFound(ApiResult<object>.Failure("404", "Assignment not found."));

        return Ok(ApiResult<bool>.Success(result, "200", "Assignment deleted successfully."));
    }
}
