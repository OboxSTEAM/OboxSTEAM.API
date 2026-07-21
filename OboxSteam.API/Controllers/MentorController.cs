using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OboxSteam.Application.Commons;
using OboxSteam.Application.DTOs.MentorDTO;
using OboxSteam.Application.Interfaces;
using OboxSteam.Application.Utils;
using Swashbuckle.AspNetCore.Annotations;

namespace OboxSteam.API.Controllers;

[Route("api/mentors")]
[ApiController]
public class MentorController : ControllerBase
{
    private readonly IMentorService _mentorService;

    public MentorController(IMentorService mentorService)
    {
        _mentorService = mentorService;
    }

    [HttpGet("me/skills")]
    [Authorize(Roles = "Mentor")]
    [SwaggerOperation(Summary = "List my mentor skills")]
    [ProducesResponseType(typeof(ApiResult<List<MentorSkillDto>>), 200)]
    public async Task<IActionResult> GetMySkills()
    {
        var result = await _mentorService.GetMySkillsAsync();
        return Ok(ApiResult<List<MentorSkillDto>>.Success(result, "200", "Skills retrieved successfully."));
    }

    [HttpPost("me/skills")]
    [Authorize(Roles = "Mentor")]
    [SwaggerOperation(Summary = "Add a skill to my mentor profile")]
    [ProducesResponseType(typeof(ApiResult<MentorSkillDto>), 201)]
    public async Task<IActionResult> AddMySkill([FromBody] CreateMentorSkillRequestDto dto)
    {
        var result = await _mentorService.AddMySkillAsync(dto);
        return StatusCode(
            StatusCodes.Status201Created,
            ApiResult<MentorSkillDto>.Success(result, "201", "Skill added successfully."));
    }

    [HttpDelete("me/skills/{id:guid}")]
    [Authorize(Roles = "Mentor")]
    [SwaggerOperation(Summary = "Remove a skill from my mentor profile")]
    [ProducesResponseType(typeof(ApiResult<bool>), 200)]
    public async Task<IActionResult> RemoveMySkill([FromRoute] Guid id)
    {
        await _mentorService.RemoveMySkillAsync(id);
        return Ok(ApiResult<bool>.Success(true, "200", "Skill removed successfully."));
    }

    [HttpGet]
    [Authorize(Roles = "SuperAdmin,Manager")]
    [SwaggerOperation(Summary = "List mentors with skills and concurrent usage")]
    [ProducesResponseType(typeof(ApiResult<Pagination<MentorProfileDto>>), 200)]
    public async Task<IActionResult> GetMentors(
        [FromQuery] string? search = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10)
    {
        if (page < 1 || pageSize < 1)
        {
            return BadRequest(ApiResult<object>.Failure("400", "Invalid pagination parameters."));
        }

        var result = await _mentorService.GetMentorsAsync(search, page, pageSize);
        return Ok(ApiResult<Pagination<MentorProfileDto>>.Success(
            result, "200", "Mentors retrieved successfully."));
    }

    [HttpGet("{id:guid}")]
    [Authorize(Roles = "SuperAdmin,Manager")]
    [SwaggerOperation(Summary = "Get mentor profile for assignment decisions")]
    [ProducesResponseType(typeof(ApiResult<MentorProfileDto>), 200)]
    public async Task<IActionResult> GetMentorProfile([FromRoute] Guid id)
    {
        var result = await _mentorService.GetMentorProfileAsync(id);
        return Ok(ApiResult<MentorProfileDto>.Success(result, "200", "Mentor profile retrieved successfully."));
    }

    [HttpPut("{id:guid}/class-limit")]
    [Authorize(Roles = "SuperAdmin,Manager")]
    [SwaggerOperation(Summary = "Set per-mentor concurrent class limit")]
    [ProducesResponseType(typeof(ApiResult<MentorProfileDto>), 200)]
    public async Task<IActionResult> SetClassLimit(
        [FromRoute] Guid id,
        [FromBody] UpdateMentorClassLimitRequestDto dto)
    {
        var result = await _mentorService.SetClassLimitAsync(id, dto);
        return Ok(ApiResult<MentorProfileDto>.Success(result, "200", "Class limit updated successfully."));
    }
}
