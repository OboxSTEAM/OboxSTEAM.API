using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OboxSteam.Application.DTOs.AuthDTO;
using OboxSteam.Application.DTOs.ParentDTO;
using OboxSteam.Application.Interfaces;
using OboxSteam.Application.Utils;

namespace OboxSteam.API.Controllers;

[Route("api/parent")]
[ApiController]
public class ParentController : ControllerBase
{
    private readonly IParentService _parentService;
    private readonly IConfiguration _configuration;

    public ParentController(IParentService parentService, IConfiguration configuration)
    {
        _parentService = parentService;
        _configuration = configuration;
    }

    [HttpPost("request-link")]
    [Authorize(Roles = "Student")] // Chỉ dành cho học sinh
    public async Task<IActionResult> RequestLink([FromBody] RequestLinkDto dto)
    {
        var result = await _parentService.RequestParentLinkAsync(dto, _configuration);
        return Ok(ApiResult<object>.Success(result, "200", "Parent link request sent successfully."));
    }

    [HttpPost("magic-login")]
    [AllowAnonymous]
    public async Task<IActionResult> MagicLogin([FromBody] MagicLoginDto dto)
    {
        var result = await _parentService.MagicLoginAsync(dto, _configuration);
        return Ok(ApiResult<LoginResponseDto>.Success(result, "200", "Logged in via Magic Link and confirmed association successfully."));
    }

    [HttpPost("complete-profile")]
    [Authorize(Roles = "Parent")]
    public async Task<IActionResult> CompleteProfile([FromBody] CompleteProfileDto dto)
    {
        var result = await _parentService.CompleteProfileAsync(dto);
        return Ok(ApiResult<object>.Success(result, "200", "Profile completed and password created successfully."));
    }

    [HttpPost("approve-link")]
    [Authorize(Roles = "Parent")]
    public async Task<IActionResult> ApproveLink([FromBody] ApproveLinkDto dto)
    {
        var result = await _parentService.ApproveLinkAsync(dto, _configuration);
        return Ok(ApiResult<object>.Success(result, "200", "Student association approved successfully."));
    }

    [HttpGet("links")]
    [Authorize(Roles = "Student,Parent")]
    public async Task<IActionResult> GetParentStudentRelations()
    {
        var result = await _parentService.GetParentStudentRelationsAsync();
        return Ok(ApiResult<List<ParentStudentRelationDto>>.Success(result, "200", "Retrieved associated accounts successfully."));
    }
}
