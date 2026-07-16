using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OboxSteam.Application.DTOs.PortfolioDTO;
using OboxSteam.Application.Interfaces;
using OboxSteam.Application.Utils;
using Swashbuckle.AspNetCore.Annotations;

namespace OboxSteam.API.Controllers;

[Route("api/portfolios")]
[ApiController]
public class PortfolioController : ControllerBase
{
    private readonly IPortfolioService _portfolioService;

    public PortfolioController(IPortfolioService portfolioService)
    {
        _portfolioService = portfolioService;
    }

    [HttpGet("me")]
    [Authorize(Roles = "Student,SuperAdmin,Manager")]
    [SwaggerOperation(
        Summary = "Get my portfolio",
        Description = "Returns the authenticated student's root portfolio with all items.")]
    [ProducesResponseType(typeof(ApiResult<PortfolioResponseDto>), 200)]
    [ProducesResponseType(typeof(ApiResult<object>), 401)]
    [ProducesResponseType(typeof(ApiResult<object>), 403)]
    [ProducesResponseType(typeof(ApiResult<object>), 404)]
    public async Task<IActionResult> GetMyPortfolio()
    {
        var result = await _portfolioService.GetMyPortfolioAsync();
        return Ok(ApiResult<PortfolioResponseDto>.Success(
            result,
            "200",
            "Portfolio retrieved successfully."));
    }

    [HttpPost]
    [Authorize(Roles = "Student")]
    [SwaggerOperation(
        Summary = "Create my portfolio",
        Description = "Creates an unpublished portfolio without a subdomain. One portfolio per student.")]
    [ProducesResponseType(typeof(ApiResult<PortfolioResponseDto>), 201)]
    [ProducesResponseType(typeof(ApiResult<object>), 401)]
    [ProducesResponseType(typeof(ApiResult<object>), 403)]
    [ProducesResponseType(typeof(ApiResult<object>), 409)]
    public async Task<IActionResult> CreateMyPortfolio()
    {
        var result = await _portfolioService.CreateMyPortfolioAsync();
        return CreatedAtAction(
            nameof(GetMyPortfolio),
            ApiResult<PortfolioResponseDto>.Success(
                result,
                "201",
                "Portfolio created successfully."));
    }

    [HttpPut("me")]
    [Authorize(Roles = "Student")]
    [SwaggerOperation(
        Summary = "Update my portfolio",
        Description = "Updates profile, theme, links, subdomain, and publish state. Publishing requires a valid unique subdomain.")]
    [ProducesResponseType(typeof(ApiResult<PortfolioResponseDto>), 200)]
    [ProducesResponseType(typeof(ApiResult<object>), 400)]
    [ProducesResponseType(typeof(ApiResult<object>), 401)]
    [ProducesResponseType(typeof(ApiResult<object>), 403)]
    [ProducesResponseType(typeof(ApiResult<object>), 404)]
    [ProducesResponseType(typeof(ApiResult<object>), 409)]
    public async Task<IActionResult> UpdateMyPortfolio(
        [FromBody, SwaggerParameter("Updated portfolio settings")] UpdatePortfolioRequestDto dto)
    {
        var result = await _portfolioService.UpdateMyPortfolioAsync(dto);
        return Ok(ApiResult<PortfolioResponseDto>.Success(
            result,
            "200",
            "Portfolio updated successfully."));
    }

    [HttpGet("subdomain-available")]
    [Authorize(Roles = "Student")]
    [SwaggerOperation(
        Summary = "Check subdomain availability",
        Description = "Live check while the student types a subdomain. Excludes the caller's current subdomain.")]
    [ProducesResponseType(typeof(ApiResult<SubdomainAvailabilityResponseDto>), 200)]
    [ProducesResponseType(typeof(ApiResult<object>), 401)]
    [ProducesResponseType(typeof(ApiResult<object>), 403)]
    [ProducesResponseType(typeof(ApiResult<object>), 404)]
    public async Task<IActionResult> CheckSubdomainAvailability(
        [FromQuery, SwaggerParameter("Subdomain label to check")] string subdomain)
    {
        var result = await _portfolioService.CheckSubdomainAvailabilityAsync(subdomain);
        return Ok(ApiResult<SubdomainAvailabilityResponseDto>.Success(
            result,
            "200",
            result.Available
                ? "Subdomain is available."
                : result.Reason ?? "Subdomain is not available."));
    }

    [HttpPost("me/items")]
    [Authorize(Roles = "Student")]
    [SwaggerOperation(
        Summary = "Add a manual portfolio item",
        Description = "Creates ExternalCert, Hobby, Extracurricular, or Project items.")]
    [ProducesResponseType(typeof(ApiResult<PortfolioCustomItemResponseDto>), 201)]
    [ProducesResponseType(typeof(ApiResult<object>), 400)]
    [ProducesResponseType(typeof(ApiResult<object>), 401)]
    [ProducesResponseType(typeof(ApiResult<object>), 403)]
    [ProducesResponseType(typeof(ApiResult<object>), 404)]
    public async Task<IActionResult> AddItem(
        [FromBody, SwaggerParameter("New portfolio item")] CreatePortfolioItemRequestDto dto)
    {
        var result = await _portfolioService.AddItemAsync(dto);
        return StatusCode(
            201,
            ApiResult<PortfolioCustomItemResponseDto>.Success(
                result,
                "201",
                "Portfolio item created successfully."));
    }

    [HttpPut("me/items/{itemId:guid}")]
    [Authorize(Roles = "Student")]
    [SwaggerOperation(
        Summary = "Update a portfolio item",
        Description = "Updates manual items fully. Auto-imported items can be hidden, reordered, and narrative-edited.")]
    [ProducesResponseType(typeof(ApiResult<PortfolioCustomItemResponseDto>), 200)]
    [ProducesResponseType(typeof(ApiResult<object>), 400)]
    [ProducesResponseType(typeof(ApiResult<object>), 401)]
    [ProducesResponseType(typeof(ApiResult<object>), 403)]
    [ProducesResponseType(typeof(ApiResult<object>), 404)]
    public async Task<IActionResult> UpdateItem(
        [FromRoute] Guid itemId,
        [FromBody, SwaggerParameter("Updated portfolio item")] UpdatePortfolioItemRequestDto dto)
    {
        var result = await _portfolioService.UpdateItemAsync(itemId, dto);
        return Ok(ApiResult<PortfolioCustomItemResponseDto>.Success(
            result,
            "200",
            "Portfolio item updated successfully."));
    }

    [HttpDelete("me/items/{itemId:guid}")]
    [Authorize(Roles = "Student")]
    [SwaggerOperation(
        Summary = "Remove a manual portfolio item",
        Description = "Soft-deletes manual items only. Auto-imported items must be hidden instead.")]
    [ProducesResponseType(typeof(ApiResult<object>), 200)]
    [ProducesResponseType(typeof(ApiResult<object>), 400)]
    [ProducesResponseType(typeof(ApiResult<object>), 401)]
    [ProducesResponseType(typeof(ApiResult<object>), 403)]
    [ProducesResponseType(typeof(ApiResult<object>), 404)]
    public async Task<IActionResult> RemoveItem([FromRoute] Guid itemId)
    {
        await _portfolioService.RemoveItemAsync(itemId);
        return Ok(ApiResult.Success("200", "Portfolio item removed successfully."));
    }

    [HttpPut("me/items/reorder")]
    [Authorize(Roles = "Student")]
    [SwaggerOperation(
        Summary = "Reorder portfolio items",
        Description = "Updates display order for the provided item ids.")]
    [ProducesResponseType(typeof(ApiResult<PortfolioResponseDto>), 200)]
    [ProducesResponseType(typeof(ApiResult<object>), 400)]
    [ProducesResponseType(typeof(ApiResult<object>), 401)]
    [ProducesResponseType(typeof(ApiResult<object>), 403)]
    [ProducesResponseType(typeof(ApiResult<object>), 404)]
    public async Task<IActionResult> ReorderItems(
        [FromBody, SwaggerParameter("Item display order updates")] ReorderPortfolioItemsRequestDto dto)
    {
        var result = await _portfolioService.ReorderItemsAsync(dto);
        return Ok(ApiResult<PortfolioResponseDto>.Success(
            result,
            "200",
            "Portfolio items reordered successfully."));
    }

    [HttpPost("me/sync")]
    [Authorize(Roles = "Student")]
    [SwaggerOperation(
        Summary = "Sync auto-imported portfolio items",
        Description = "Idempotently imports certificates and graded capstone projects. Highlight reels are not imported.")]
    [ProducesResponseType(typeof(ApiResult<PortfolioResponseDto>), 200)]
    [ProducesResponseType(typeof(ApiResult<object>), 401)]
    [ProducesResponseType(typeof(ApiResult<object>), 403)]
    [ProducesResponseType(typeof(ApiResult<object>), 404)]
    public async Task<IActionResult> SyncMyPortfolio()
    {
        var result = await _portfolioService.SyncMyPortfolioAsync();
        return Ok(ApiResult<PortfolioResponseDto>.Success(
            result,
            "200",
            "Portfolio synced successfully."));
    }

    [HttpGet("by-subdomain/{subdomain}")]
    [AllowAnonymous]
    [SwaggerOperation(
        Summary = "Get public portfolio by subdomain",
        Description = "Anonymous endpoint for the public portfolio page. Returns only visible items.")]
    [ProducesResponseType(typeof(ApiResult<PublicPortfolioResponseDto>), 200)]
    [ProducesResponseType(typeof(ApiResult<object>), 404)]
    public async Task<IActionResult> GetPublicPortfolioBySubdomain([FromRoute] string subdomain)
    {
        var result = await _portfolioService.GetPublicPortfolioBySubdomainAsync(subdomain);
        return Ok(ApiResult<PublicPortfolioResponseDto>.Success(
            result,
            "200",
            "Public portfolio retrieved successfully."));
    }
}
