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
        Description = "Owning expert only. Updates the current draft; published versions are immutable.")]
    [ProducesResponseType(typeof(ApiResult<ProgramFrameworkResponseDto>), 200)]
    [ProducesResponseType(typeof(ApiResult<object>), 400)]
    [ProducesResponseType(typeof(ApiResult<object>), 403)]
    [ProducesResponseType(typeof(ApiResult<object>), 404)]
    [ProducesResponseType(typeof(ApiResult<object>), 409)]
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
        Description = "Owning expert only. Unlinks the attached Draft program. Blocked with 409 when the attached program is not Draft.")]
    [ProducesResponseType(typeof(ApiResult<bool>), 200)]
    [ProducesResponseType(typeof(ApiResult<object>), 403)]
    [ProducesResponseType(typeof(ApiResult<object>), 404)]
    [ProducesResponseType(typeof(ApiResult<object>), 409)]
    public async Task<IActionResult> DeleteFramework([FromRoute] Guid id)
    {
        var result = await _frameworkService.DeleteFrameworkAsync(id);
        return Ok(ApiResult<bool>.Success(result, "200", "Program framework deleted successfully."));
    }

    [HttpPost("{id:guid}/archive")]
    [Authorize(Roles = "Expert")]
    [SwaggerOperation(Summary = "Archive a framework", Description = "Prevents new assignments while preserving existing pinned programs and history.")]
    public async Task<IActionResult> ArchiveFramework([FromRoute] Guid id)
    {
        var result = await _frameworkService.ArchiveFrameworkAsync(id);
        return Ok(ApiResult<ProgramFrameworkResponseDto>.Success(result, "200", "Program framework archived."));
    }

    [HttpGet("{id:guid}/versions")]
    public async Task<IActionResult> GetVersions([FromRoute] Guid id)
    {
        var result = await _frameworkService.GetVersionsAsync(id);
        return Ok(ApiResult<IReadOnlyList<ProgramFrameworkVersionResponseDto>>.Success(result, "200", "Framework versions retrieved."));
    }

    [HttpGet("{id:guid}/versions/{versionId:guid}")]
    public async Task<IActionResult> GetVersion([FromRoute] Guid id, [FromRoute] Guid versionId)
    {
        var result = await _frameworkService.GetVersionAsync(id, versionId);
        return Ok(ApiResult<ProgramFrameworkVersionResponseDto>.Success(result, "200", "Framework version retrieved."));
    }

    [HttpPost("{id:guid}/versions/draft")]
    [Authorize(Roles = "Expert")]
    public async Task<IActionResult> CreateDraftVersion([FromRoute] Guid id)
    {
        var result = await _frameworkService.CreateDraftVersionAsync(id);
        return CreatedAtAction(nameof(GetVersion), new { id, versionId = result.Id },
            ApiResult<ProgramFrameworkVersionResponseDto>.Success(result, "201", "Draft framework version created."));
    }

    [HttpPost("{id:guid}/versions/{versionId:guid}/publish")]
    [Authorize(Roles = "Expert")]
    public async Task<IActionResult> PublishVersion([FromRoute] Guid id, [FromRoute] Guid versionId)
    {
        var result = await _frameworkService.PublishDraftVersionAsync(id, versionId);
        return Ok(ApiResult<ProgramFrameworkVersionResponseDto>.Success(result, "200", "Framework version published."));
    }

    [HttpPut("{id:guid}/versions/{versionId:guid}/rubric")]
    [Authorize(Roles = "Expert")]
    [SwaggerOperation(Summary = "Replace a draft rubric atomically")]
    public async Task<IActionResult> SaveDraftRubric(
        [FromRoute] Guid id,
        [FromRoute] Guid versionId,
        [FromBody] SaveFrameworkRubricRequest request)
    {
        var result = await _frameworkService.SaveDraftRubricAsync(id, versionId, request);
        return Ok(ApiResult<ProgramFrameworkVersionResponseDto>.Success(result, "200", "Draft rubric saved."));
    }

    [HttpPost("{id:guid}/criteria")]
    [SwaggerOperation(
        Summary = "Add a rubric criterion to a program framework",
        Description = "Owning expert only. Allowed when the framework is unattached or the attached program is Draft. Blocked with 409 otherwise.")]
    [ProducesResponseType(typeof(ApiResult<FrameworkRubricCriterionResponseDto>), 201)]
    [ProducesResponseType(typeof(ApiResult<object>), 400)]
    [ProducesResponseType(typeof(ApiResult<object>), 403)]
    [ProducesResponseType(typeof(ApiResult<object>), 404)]
    [ProducesResponseType(typeof(ApiResult<object>), 409)]
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
    [SwaggerOperation(
        Summary = "Update a rubric criterion",
        Description = "Owning expert only. Allowed when the framework is unattached or the attached program is Draft. Blocked with 409 otherwise.")]
    [ProducesResponseType(typeof(ApiResult<FrameworkRubricCriterionResponseDto>), 200)]
    [ProducesResponseType(typeof(ApiResult<object>), 400)]
    [ProducesResponseType(typeof(ApiResult<object>), 403)]
    [ProducesResponseType(typeof(ApiResult<object>), 404)]
    [ProducesResponseType(typeof(ApiResult<object>), 409)]
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
    [SwaggerOperation(
        Summary = "Delete a rubric criterion",
        Description = "Owning expert only. Allowed when the framework is unattached or the attached program is Draft. Blocked with 409 otherwise.")]
    [ProducesResponseType(typeof(ApiResult<bool>), 200)]
    [ProducesResponseType(typeof(ApiResult<object>), 403)]
    [ProducesResponseType(typeof(ApiResult<object>), 404)]
    [ProducesResponseType(typeof(ApiResult<object>), 409)]
    public async Task<IActionResult> DeleteCriterion([FromRoute] Guid id, [FromRoute] Guid criterionId)
    {
        var result = await _frameworkService.DeleteCriterionAsync(id, criterionId);
        return Ok(ApiResult<bool>.Success(result, "200", "Criterion deleted successfully."));
    }
}
