using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OboxSteam.Application.DTOs.ResearchMilestoneDTO;
using OboxSteam.Application.Interfaces;
using OboxSteam.Application.Utils;
using Swashbuckle.AspNetCore.Annotations;

namespace OboxSteam.API.Controllers;

[Route("api")]
[ApiController]
public sealed class ResearchMilestoneController : ControllerBase
{
    private readonly IResearchMilestoneService _researchMilestoneService;

    public ResearchMilestoneController(IResearchMilestoneService researchMilestoneService)
    {
        _researchMilestoneService = researchMilestoneService;
    }

    // =========================================================================
    // CREATE  —  POST /api/modules/{moduleId}/research-milestones   [SuperAdmin,Manager]
    // =========================================================================

    [HttpPost("modules/{moduleId:guid}/research-milestones")]
    [Authorize(Roles = "SuperAdmin,Manager")]
    [SwaggerOperation(
        Summary = "Create a research milestone",
        Description = "Creates a research milestone and its linked deliverable assignment. Requires SuperAdmin or Manager.")]
    [ProducesResponseType(typeof(ApiResult<ResearchMilestoneResponseDto>), 201)]
    [ProducesResponseType(typeof(ApiResult<object>), 400)]
    [ProducesResponseType(typeof(ApiResult<object>), 401)]
    [ProducesResponseType(typeof(ApiResult<object>), 403)]
    [ProducesResponseType(typeof(ApiResult<object>), 404)]
    [ProducesResponseType(typeof(ApiResult<object>), 409)]
    public async Task<IActionResult> CreateMilestone(
        [FromRoute] Guid moduleId,
        [FromBody, SwaggerParameter("Milestone create request")] CreateResearchMilestoneRequestDto request)
    {
        var result = await _researchMilestoneService.CreateMilestone(moduleId, request);

        return CreatedAtAction(
            nameof(GetMilestoneById),
            new { milestoneId = result.Id },
            ApiResult<ResearchMilestoneResponseDto>.Success(result, "201", "Research milestone created successfully."));
    }

    // =========================================================================
    // GET BY ID  —  GET /api/research-milestones/{milestoneId}
    // =========================================================================

    [HttpGet("research-milestones/{milestoneId:guid}")]
    [SwaggerOperation(
        Summary = "Get research milestone by ID",
        Description = "Retrieve a research milestone (includes assignment and linked activities).")]
    [ProducesResponseType(typeof(ApiResult<ResearchMilestoneResponseDto>), 200)]
    [ProducesResponseType(typeof(ApiResult<object>), 404)]
    public async Task<IActionResult> GetMilestoneById([FromRoute] Guid milestoneId)
    {
        var result = await _researchMilestoneService.GetMilestoneById(milestoneId);
        if (result == null)
        {
            return NotFound(ApiResult<object>.Failure("404", "Research milestone not found."));
        }

        return Ok(ApiResult<ResearchMilestoneResponseDto>.Success(
            result,
            "200",
            "Research milestone retrieved successfully."));
    }

    // =========================================================================
    // GET BY MODULE  —  GET /api/modules/{moduleId}/research-milestones
    // =========================================================================

    [HttpGet("modules/{moduleId:guid}/research-milestones")]
    [SwaggerOperation(
        Summary = "Get research milestones by module",
        Description = "Lists all research milestones for a module, ordered by MilestoneOrder.")]
    [ProducesResponseType(typeof(ApiResult<List<ResearchMilestoneResponseDto>>), 200)]
    [ProducesResponseType(typeof(ApiResult<object>), 404)]
    public async Task<IActionResult> GetMilestonesByModule([FromRoute] Guid moduleId)
    {
        var result = await _researchMilestoneService.GetMilestonesByModule(moduleId);

        return Ok(ApiResult<List<ResearchMilestoneResponseDto>>.Success(
            result,
            "200",
            "Research milestones retrieved successfully."));
    }

    // =========================================================================
    // UPDATE  —  PUT /api/research-milestones/{milestoneId}   [SuperAdmin,Manager]
    // =========================================================================

    [HttpPut("research-milestones/{milestoneId:guid}")]
    [Authorize(Roles = "SuperAdmin,Manager")]
    [SwaggerOperation(
        Summary = "Update a research milestone",
        Description = "Updates milestone fields and the linked deliverable assignment. Requires SuperAdmin or Manager.")]
    [ProducesResponseType(typeof(ApiResult<ResearchMilestoneResponseDto>), 200)]
    [ProducesResponseType(typeof(ApiResult<object>), 400)]
    [ProducesResponseType(typeof(ApiResult<object>), 401)]
    [ProducesResponseType(typeof(ApiResult<object>), 403)]
    [ProducesResponseType(typeof(ApiResult<object>), 404)]
    [ProducesResponseType(typeof(ApiResult<object>), 409)]
    public async Task<IActionResult> UpdateMilestone(
        [FromRoute] Guid milestoneId,
        [FromBody, SwaggerParameter("Milestone update request")] UpdateResearchMilestoneRequestDto request)
    {
        var result = await _researchMilestoneService.UpdateMilestone(milestoneId, request);
        if (result == null)
        {
            return NotFound(ApiResult<object>.Failure("404", "Research milestone not found."));
        }

        return Ok(ApiResult<ResearchMilestoneResponseDto>.Success(result, "200", "Research milestone updated successfully."));
    }

    // =========================================================================
    // DELETE  —  DELETE /api/research-milestones/{milestoneId}   [SuperAdmin,Manager]
    // =========================================================================

    [HttpDelete("research-milestones/{milestoneId:guid}")]
    [Authorize(Roles = "SuperAdmin,Manager")]
    [SwaggerOperation(
        Summary = "Delete a research milestone",
        Description = "Soft-deletes a milestone (and its linked assignment) when no submissions exist. Requires SuperAdmin or Manager.")]
    [ProducesResponseType(typeof(ApiResult<object>), 200)]
    [ProducesResponseType(typeof(ApiResult<object>), 401)]
    [ProducesResponseType(typeof(ApiResult<object>), 403)]
    [ProducesResponseType(typeof(ApiResult<object>), 404)]
    [ProducesResponseType(typeof(ApiResult<object>), 409)]
    public async Task<IActionResult> DeleteMilestone([FromRoute] Guid milestoneId)
    {
        var deleted = await _researchMilestoneService.DeleteMilestone(milestoneId);
        if (!deleted)
        {
            return NotFound(ApiResult<object>.Failure("404", "Research milestone not found."));
        }

        return Ok(ApiResult<object>.Success(new { }, "200", "Research milestone deleted successfully."));
    }

    // =========================================================================
    // LINK ACTIVITY  —  POST /api/research-milestones/{milestoneId}/activities   [SuperAdmin,Manager,Mentor]
    // =========================================================================

    [HttpPost("research-milestones/{milestoneId:guid}/activities")]
    [Authorize(Roles = "SuperAdmin,Manager,Mentor")]
    [SwaggerOperation(
        Summary = "Link an activity to a research milestone",
        Description = "Links an existing activity to a milestone with required flag and display order. Requires SuperAdmin, Manager, or Mentor.")]
    [ProducesResponseType(typeof(ApiResult<ResearchMilestoneActivityResponseDto>), 201)]
    [ProducesResponseType(typeof(ApiResult<object>), 400)]
    [ProducesResponseType(typeof(ApiResult<object>), 401)]
    [ProducesResponseType(typeof(ApiResult<object>), 403)]
    [ProducesResponseType(typeof(ApiResult<object>), 404)]
    [ProducesResponseType(typeof(ApiResult<object>), 409)]
    public async Task<IActionResult> LinkActivity(
        [FromRoute] Guid milestoneId,
        [FromBody, SwaggerParameter("Link request")] LinkMilestoneActivityRequestDto request)
    {
        var result = await _researchMilestoneService.LinkActivity(milestoneId, request);

        return CreatedAtAction(
            nameof(GetMilestoneById),
            new { milestoneId },
            ApiResult<ResearchMilestoneActivityResponseDto>.Success(result, "201", "Activity linked successfully."));
    }

    // =========================================================================
    // UPDATE LINK  —  PUT /api/research-milestones/{milestoneId}/activities/{activityId}   [SuperAdmin,Manager,Mentor]
    // =========================================================================

    [HttpPut("research-milestones/{milestoneId:guid}/activities/{activityId:guid}")]
    [Authorize(Roles = "SuperAdmin,Manager,Mentor")]
    [SwaggerOperation(
        Summary = "Update milestone activity link",
        Description = "Updates required flag and/or display order for the activity link. Requires SuperAdmin, Manager, or Mentor.")]
    [ProducesResponseType(typeof(ApiResult<ResearchMilestoneActivityResponseDto>), 200)]
    [ProducesResponseType(typeof(ApiResult<object>), 400)]
    [ProducesResponseType(typeof(ApiResult<object>), 401)]
    [ProducesResponseType(typeof(ApiResult<object>), 403)]
    [ProducesResponseType(typeof(ApiResult<object>), 404)]
    public async Task<IActionResult> UpdateActivityLink(
        [FromRoute] Guid milestoneId,
        [FromRoute] Guid activityId,
        [FromBody, SwaggerParameter("Update link request")] UpdateMilestoneActivityLinkRequestDto request)
    {
        var result = await _researchMilestoneService.UpdateActivityLink(milestoneId, activityId, request);
        if (result == null)
        {
            return NotFound(ApiResult<object>.Failure("404", "Milestone activity link not found."));
        }

        return Ok(ApiResult<ResearchMilestoneActivityResponseDto>.Success(
            result,
            "200",
            "Activity link updated successfully."));
    }

    // =========================================================================
    // UNLINK ACTIVITY  —  DELETE /api/research-milestones/{milestoneId}/activities/{activityId}   [SuperAdmin,Manager,Mentor]
    // =========================================================================

    [HttpDelete("research-milestones/{milestoneId:guid}/activities/{activityId:guid}")]
    [Authorize(Roles = "SuperAdmin,Manager,Mentor")]
    [SwaggerOperation(
        Summary = "Unlink activity from a research milestone",
        Description = "Removes (soft-deletes) the activity link from the milestone. Requires SuperAdmin, Manager, or Mentor.")]
    [ProducesResponseType(typeof(ApiResult<object>), 200)]
    [ProducesResponseType(typeof(ApiResult<object>), 401)]
    [ProducesResponseType(typeof(ApiResult<object>), 403)]
    [ProducesResponseType(typeof(ApiResult<object>), 404)]
    public async Task<IActionResult> UnlinkActivity(
        [FromRoute] Guid milestoneId,
        [FromRoute] Guid activityId)
    {
        var removed = await _researchMilestoneService.UnlinkActivity(milestoneId, activityId);
        if (!removed)
        {
            return NotFound(ApiResult<object>.Failure("404", "Milestone activity link not found."));
        }

        return Ok(ApiResult<object>.Success(new { }, "200", "Activity unlinked successfully."));
    }

    // =========================================================================
    // PROGRESS  —  GET /api/module-enrollments/{moduleEnrollmentId}/research-milestones/progress
    // =========================================================================

    [HttpGet("module-enrollments/{moduleEnrollmentId:guid}/research-milestones/progress")]
    [Authorize(Roles = "Student,Parent,SuperAdmin,Manager")]
    [SwaggerOperation(
        Summary = "Get student research milestone progress",
        Description = "Returns per-milestone unlock and submission readiness for a module enrollment. Access is enforced per role.")]
    [ProducesResponseType(typeof(ApiResult<StudentMilestoneProgressDto>), 200)]
    [ProducesResponseType(typeof(ApiResult<object>), 401)]
    [ProducesResponseType(typeof(ApiResult<object>), 403)]
    [ProducesResponseType(typeof(ApiResult<object>), 404)]
    public async Task<IActionResult> GetStudentMilestoneProgress([FromRoute] Guid moduleEnrollmentId)
    {
        var result = await _researchMilestoneService.GetStudentMilestoneProgress(moduleEnrollmentId);

        return Ok(ApiResult<StudentMilestoneProgressDto>.Success(
            result,
            "200",
            "Milestone progress retrieved successfully."));
    }
}

