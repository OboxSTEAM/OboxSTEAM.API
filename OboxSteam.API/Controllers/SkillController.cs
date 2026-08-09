using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OboxSteam.Application.Commons;
using OboxSteam.Application.DTOs.SkillDTO;
using OboxSteam.Application.Interfaces;
using OboxSteam.Application.Utils;
using OboxSteam.Domain.Enums;
using Swashbuckle.AspNetCore.Annotations;

namespace OboxSteam.API.Controllers;

[Route("api/skills")]
[ApiController]
public sealed class SkillController : ControllerBase
{
    private readonly ISkillService _skillService;

    public SkillController(ISkillService skillService)
    {
        _skillService = skillService;
    }

    [HttpGet]
    [Authorize(Roles = "Mentor,Manager,Admin")]
    [SwaggerOperation(
        Summary = "List skill catalog",
        Description = "Paged STEAM skill catalog for class requiredSkillIds and mentor skill pickers. Soft-deleted skills are excluded.")]
    [ProducesResponseType(typeof(ApiResult<Pagination<SkillSummaryDto>>), 200)]
    [ProducesResponseType(typeof(ApiResult<object>), 400)]
    [ProducesResponseType(typeof(ApiResult<object>), 401)]
    [ProducesResponseType(typeof(ApiResult<object>), 403)]
    public async Task<IActionResult> GetSkills(
        [FromQuery, SwaggerParameter(Description = "Search by code, name, or subcategory (optional)")] string? search = null,
        [FromQuery, SwaggerParameter(Description = "Filter by category: Science, Technology, Engineering, Arts, Math, SoftSkill (optional)")] SkillCategory? category = null,
        [FromQuery, SwaggerParameter(Description = "Page number, starting from 1")] int page = 1,
        [FromQuery, SwaggerParameter(Description = "Number of items per page (1-100)")] int pageSize = 50,
        [FromQuery, SwaggerParameter(Description = "Sort by field: name, code, category, createdAt. Default: name")] string sortBy = "name",
        [FromQuery, SwaggerParameter(Description = "Sort in descending order? Default: false")] bool isDescending = false)
    {
        if (page < 1 || pageSize < 1 || pageSize > 100)
            return BadRequest(ApiResult<object>.Failure("400", "Invalid pagination parameters."));

        var result = await _skillService.GetSkills(
            search, category, page, pageSize, sortBy, isDescending);

        return Ok(ApiResult<Pagination<SkillSummaryDto>>.Success(
            result, "200", "Skills retrieved successfully."));
    }
}
