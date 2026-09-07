using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OboxSteam.Application.Commons;
using OboxSteam.Application.DTOs.ProgramFrameworkDTO;
using OboxSteam.Application.Interfaces;
using OboxSteam.Application.Utils;
using OboxSteam.Domain.Enums;
using Swashbuckle.AspNetCore.Annotations;

namespace OboxSteam.API.Controllers;

[Route("api/program-frameworks")]
[ApiController]
[Authorize(Roles = "Expert,Manager,Admin")]
public class ProgramFrameworkController : ControllerBase
{
    private readonly IProgramFrameworkService _frameworkService;

    public ProgramFrameworkController(IProgramFrameworkService frameworkService)
    {
        _frameworkService = frameworkService;
    }

    [HttpGet]
    [SwaggerOperation(
        Summary = "List program frameworks",
        Description = "Experts see their own blueprints. Manager and Admin see all. Category filter is a hint only — it does not require programs to match.")]
    [ProducesResponseType(typeof(ApiResult<Pagination<ProgramFrameworkResponseDto>>), 200)]
    [ProducesResponseType(typeof(ApiResult<object>), 400)]
    public async Task<IActionResult> GetFrameworks(
        [FromQuery] string? search = null,
        [FromQuery] ProgramCategory? category = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10)
    {
        if (page < 1 || pageSize < 1)
        {
            return BadRequest(ApiResult<object>.Failure("400", "Invalid pagination parameters."));
        }

        var result = await _frameworkService.GetFrameworksAsync(search, category, page, pageSize);
        return Ok(ApiResult<Pagination<ProgramFrameworkResponseDto>>.Success(
            result, "200", "Program frameworks retrieved successfully."));
    }

    [HttpGet("{id:guid}")]
    [SwaggerOperation(Summary = "Get a program framework with rubric criteria")]
    [ProducesResponseType(typeof(ApiResult<ProgramFrameworkResponseDto>), 200)]
    [ProducesResponseType(typeof(ApiResult<object>), 404)]
    public async Task<IActionResult> GetFrameworkById([FromRoute] Guid id)
    {
        var result = await _frameworkService.GetFrameworkByIdAsync(id);
        return Ok(ApiResult<ProgramFrameworkResponseDto>.Success(
            result, "200", "Program framework retrieved successfully."));
    }

    [HttpPost]
    [Authorize(Roles = "Expert")]
    [SwaggerOperation(
        Summary = "Create a program framework",
        Description = "Expert-owned blueprint. Opt-in rules (null = not enforced). Zero rubric criteria is allowed.")]
    [ProducesResponseType(typeof(ApiResult<ProgramFrameworkResponseDto>), 201)]
    [ProducesResponseType(typeof(ApiResult<object>), 400)]
    [ProducesResponseType(typeof(ApiResult<object>), 403)]
    public async Task<IActionResult> CreateFramework([FromBody] CreateProgramFrameworkRequest request)
    {
        var result = await _frameworkService.CreateFrameworkAsync(request);
        return CreatedAtAction(
            nameof(GetFrameworkById),
            new { id = result.Id },
            ApiResult<ProgramFrameworkResponseDto>.Success(result, "201", "Program framework created successfully."));
    }

    [HttpPut("{id:guid}")]
    [SwaggerOperation(
        Summary = "Update a program framework",
        Description = "Owning expert, or Manager/Admin override. Allowed while attached programs are PendingReview.")]
    [ProducesResponseType(typeof(ApiResult<ProgramFrameworkResponseDto>), 200)]
    [ProducesResponseType(typeof(ApiResult<object>), 400)]
    [ProducesResponseType(typeof(ApiResult<object>), 403)]
    [ProducesResponseType(typeof(ApiResult<object>), 404)]
    public async Task<IActionResult> UpdateFramework(
        [FromRoute] Guid id,
        [FromBody] UpdateProgramFrameworkRequest request)
    {
        var result = await _frameworkService.UpdateFrameworkAsync(id, request);
        return Ok(ApiResult<ProgramFrameworkResponseDto>.Success(
            result, "200", "Program framework updated successfully."));
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "Expert")]
    [SwaggerOperation(
        Summary = "Delete a program framework",
        Description = "Owning expert only. Attached programs are unlinked (free-form review).")]
    [ProducesResponseType(typeof(ApiResult<bool>), 200)]
    [ProducesResponseType(typeof(ApiResult<object>), 403)]
    [ProducesResponseType(typeof(ApiResult<object>), 404)]
    public async Task<IActionResult> DeleteFramework([FromRoute] Guid id)
    {
        var result = await _frameworkService.DeleteFrameworkAsync(id);
        return Ok(ApiResult<bool>.Success(result, "200", "Program framework deleted successfully."));
    }

    [HttpPost("{id:guid}/criteria")]
    [SwaggerOperation(Summary = "Add a rubric criterion to a program framework")]
    [ProducesResponseType(typeof(ApiResult<FrameworkRubricCriterionResponseDto>), 201)]
    [ProducesResponseType(typeof(ApiResult<object>), 400)]
    [ProducesResponseType(typeof(ApiResult<object>), 403)]
    [ProducesResponseType(typeof(ApiResult<object>), 404)]
    public async Task<IActionResult> AddCriterion(
        [FromRoute] Guid id,
        [FromBody] FrameworkRubricCriterionRequest request)
    {
        var result = await _frameworkService.AddCriterionAsync(id, request);
        return CreatedAtAction(
            nameof(GetFrameworkById),
            new { id },
            ApiResult<FrameworkRubricCriterionResponseDto>.Success(result, "201", "Criterion added successfully."));
    }

    [HttpPut("{id:guid}/criteria/{criterionId:guid}")]
    [SwaggerOperation(Summary = "Update a rubric criterion")]
    [ProducesResponseType(typeof(ApiResult<FrameworkRubricCriterionResponseDto>), 200)]
    [ProducesResponseType(typeof(ApiResult<object>), 400)]
    [ProducesResponseType(typeof(ApiResult<object>), 403)]
    [ProducesResponseType(typeof(ApiResult<object>), 404)]
    public async Task<IActionResult> UpdateCriterion(
        [FromRoute] Guid id,
        [FromRoute] Guid criterionId,
        [FromBody] FrameworkRubricCriterionRequest request)
    {
        var result = await _frameworkService.UpdateCriterionAsync(id, criterionId, request);
        return Ok(ApiResult<FrameworkRubricCriterionResponseDto>.Success(
            result, "200", "Criterion updated successfully."));
    }

    [HttpDelete("{id:guid}/criteria/{criterionId:guid}")]
    [SwaggerOperation(Summary = "Delete a rubric criterion")]
    [ProducesResponseType(typeof(ApiResult<bool>), 200)]
    [ProducesResponseType(typeof(ApiResult<object>), 403)]
    [ProducesResponseType(typeof(ApiResult<object>), 404)]
    public async Task<IActionResult> DeleteCriterion([FromRoute] Guid id, [FromRoute] Guid criterionId)
    {
        var result = await _frameworkService.DeleteCriterionAsync(id, criterionId);
        return Ok(ApiResult<bool>.Success(result, "200", "Criterion deleted successfully."));
    }
}
