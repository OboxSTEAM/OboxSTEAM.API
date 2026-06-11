using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OboxSteam.Application.DTOs.AssignmentDTO;
using OboxSteam.Application.Interfaces;
using OboxSteam.Application.Utils;
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

    [HttpGet("{assignmentId:guid}")]
    [SwaggerOperation(
        Summary = "Get assignment by ID",
        Description = "Retrieve an assignment by its ID. Students must have an active enrollment in the assignment's module.")]
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

    [HttpPost]
    [Authorize(Roles = "SuperAdmin,Manager")]
    [SwaggerOperation(
        Summary = "Create an assignment",
        Description = "Creates a new assignment for a module. Requires SuperAdmin or Manager role.")]
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
    [Authorize(Roles = "SuperAdmin,Manager")]
    [SwaggerOperation(
        Summary = "Update an assignment",
        Description = "Updates an assignment by its ID. Requires SuperAdmin or Manager role.")]
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
    [Authorize(Roles = "SuperAdmin,Manager")]
    [SwaggerOperation(
        Summary = "Delete an assignment",
        Description = "Soft-deletes an assignment by its ID. Requires SuperAdmin or Manager role.")]
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
