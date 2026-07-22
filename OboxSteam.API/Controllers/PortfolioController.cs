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
        Description = "Returns the authenticated student's editable draft portfolio with all items and sections.")]
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
        Description = "Updates portfolio profile, theme, avatar, cover image, and links. Subdomain and publication state use dedicated endpoints.")]
    [ProducesResponseType(typeof(ApiResult<PortfolioResponseDto>), 200)]
    [ProducesResponseType(typeof(ApiResult<object>), 400)]
    [ProducesResponseType(typeof(ApiResult<object>), 401)]
    [ProducesResponseType(typeof(ApiResult<object>), 403)]
    [ProducesResponseType(typeof(ApiResult<object>), 404)]
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

    [HttpPut("me/subdomain")]
    [Authorize(Roles = "Student")]
    [SwaggerOperation(
        Summary = "Claim or change my portfolio subdomain",
        Description = "Validates and claims a unique subdomain. Send null or blank to remove it while unpublished.")]
    [ProducesResponseType(typeof(ApiResult<PortfolioResponseDto>), 200)]
    [ProducesResponseType(typeof(ApiResult<object>), 400)]
    [ProducesResponseType(typeof(ApiResult<object>), 401)]
    [ProducesResponseType(typeof(ApiResult<object>), 403)]
    [ProducesResponseType(typeof(ApiResult<object>), 404)]
    [ProducesResponseType(typeof(ApiResult<object>), 409)]
    public async Task<IActionResult> UpdateMySubdomain(
        [FromBody, SwaggerParameter("Subdomain to claim or remove")] UpdatePortfolioSubdomainRequestDto dto)
    {
        var result = await _portfolioService.UpdateMySubdomainAsync(dto);
        return Ok(ApiResult<PortfolioResponseDto>.Success(
            result,
            "200",
            result.Subdomain == null
                ? "Portfolio subdomain removed successfully."
                : "Portfolio subdomain updated successfully."));
    }

    [HttpPut("me/publication")]
    [Authorize(Roles = "Student")]
    [SwaggerOperation(
        Summary = "Publish or unpublish my portfolio",
        Description = "On publish, snapshots the draft for public serving. On unpublish, removes public availability while retaining the last snapshot.")]
    [ProducesResponseType(typeof(ApiResult<PortfolioResponseDto>), 200)]
    [ProducesResponseType(typeof(ApiResult<object>), 400)]
    [ProducesResponseType(typeof(ApiResult<object>), 401)]
    [ProducesResponseType(typeof(ApiResult<object>), 403)]
    [ProducesResponseType(typeof(ApiResult<object>), 404)]
    [ProducesResponseType(typeof(ApiResult<object>), 409)]
    public async Task<IActionResult> UpdateMyPublication(
        [FromBody, SwaggerParameter("Desired publication state")] UpdatePortfolioPublicationRequestDto dto)
    {
        var result = await _portfolioService.UpdateMyPublicationAsync(dto);
        return Ok(ApiResult<PortfolioResponseDto>.Success(
            result,
            "200",
            result.IsPublic
                ? "Portfolio published successfully."
                : "Portfolio unpublished successfully."));
    }

    [HttpPost("me/media")]
    [Authorize(Roles = "Student")]
    [SwaggerOperation(
        Summary = "Upload portfolio media",
        Description = "Uploads a portfolio-scoped image (jpg/jpeg/png, max 5 MB) to blob storage.")]
    [ProducesResponseType(typeof(ApiResult<PortfolioMediaUploadResponseDto>), 201)]
    [ProducesResponseType(typeof(ApiResult<object>), 400)]
    [ProducesResponseType(typeof(ApiResult<object>), 401)]
    [ProducesResponseType(typeof(ApiResult<object>), 403)]
    [ProducesResponseType(typeof(ApiResult<object>), 404)]
    public async Task<IActionResult> UploadMedia(IFormFile file)
    {
        var result = await _portfolioService.UploadMediaAsync(file);
        return StatusCode(
            201,
            ApiResult<PortfolioMediaUploadResponseDto>.Success(
                result,
                "201",
                "Portfolio media uploaded successfully."));
    }

    [HttpGet("me/media")]
    [Authorize(Roles = "Student")]
    [SwaggerOperation(
        Summary = "List my portfolio media",
        Description = "Returns media assets uploaded to the caller's portfolio.")]
    [ProducesResponseType(typeof(ApiResult<List<PortfolioMediaUploadResponseDto>>), 200)]
    [ProducesResponseType(typeof(ApiResult<object>), 401)]
    [ProducesResponseType(typeof(ApiResult<object>), 403)]
    [ProducesResponseType(typeof(ApiResult<object>), 404)]
    public async Task<IActionResult> ListMedia()
    {
        var result = await _portfolioService.ListMediaAsync();
        return Ok(ApiResult<List<PortfolioMediaUploadResponseDto>>.Success(
            result,
            "200",
            "Portfolio media listed successfully."));
    }

    [HttpDelete("me/media/{mediaId:guid}")]
    [Authorize(Roles = "Student")]
    [SwaggerOperation(
        Summary = "Delete portfolio media",
        Description = "Soft-deletes a portfolio media asset that is not referenced by any item or section gallery.")]
    [ProducesResponseType(typeof(ApiResult<object>), 200)]
    [ProducesResponseType(typeof(ApiResult<object>), 400)]
    [ProducesResponseType(typeof(ApiResult<object>), 401)]
    [ProducesResponseType(typeof(ApiResult<object>), 403)]
    [ProducesResponseType(typeof(ApiResult<object>), 404)]
    public async Task<IActionResult> DeleteMedia([FromRoute] Guid mediaId)
    {
        await _portfolioService.DeleteMediaAsync(mediaId);
        return Ok(ApiResult.Success("200", "Portfolio media deleted successfully."));
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

    [HttpPost("me/sections")]
    [Authorize(Roles = "Student")]
    [SwaggerOperation(
        Summary = "Create a custom portfolio section",
        Description = "Creates RichText, Gallery, or Embed blocks. Built-in group sections are seeded automatically.")]
    [ProducesResponseType(typeof(ApiResult<PortfolioSectionResponseDto>), 201)]
    [ProducesResponseType(typeof(ApiResult<object>), 400)]
    [ProducesResponseType(typeof(ApiResult<object>), 401)]
    [ProducesResponseType(typeof(ApiResult<object>), 403)]
    [ProducesResponseType(typeof(ApiResult<object>), 404)]
    public async Task<IActionResult> CreateSection(
        [FromBody, SwaggerParameter("New custom section")] CreatePortfolioSectionRequestDto dto)
    {
        var result = await _portfolioService.CreateSectionAsync(dto);
        return StatusCode(
            201,
            ApiResult<PortfolioSectionResponseDto>.Success(
                result,
                "201",
                "Portfolio section created successfully."));
    }

    [HttpPut("me/sections/{sectionId:guid}")]
    [Authorize(Roles = "Student")]
    [SwaggerOperation(
        Summary = "Update a portfolio section",
        Description = "Updates custom blocks fully. Built-in group sections can be hidden, reordered, and retitled.")]
    [ProducesResponseType(typeof(ApiResult<PortfolioSectionResponseDto>), 200)]
    [ProducesResponseType(typeof(ApiResult<object>), 400)]
    [ProducesResponseType(typeof(ApiResult<object>), 401)]
    [ProducesResponseType(typeof(ApiResult<object>), 403)]
    [ProducesResponseType(typeof(ApiResult<object>), 404)]
    public async Task<IActionResult> UpdateSection(
        [FromRoute] Guid sectionId,
        [FromBody, SwaggerParameter("Updated section")] UpdatePortfolioSectionRequestDto dto)
    {
        var result = await _portfolioService.UpdateSectionAsync(sectionId, dto);
        return Ok(ApiResult<PortfolioSectionResponseDto>.Success(
            result,
            "200",
            "Portfolio section updated successfully."));
    }

    [HttpDelete("me/sections/{sectionId:guid}")]
    [Authorize(Roles = "Student")]
    [SwaggerOperation(
        Summary = "Delete a custom portfolio section",
        Description = "Soft-deletes custom blocks only. Built-in group sections must be hidden instead.")]
    [ProducesResponseType(typeof(ApiResult<object>), 200)]
    [ProducesResponseType(typeof(ApiResult<object>), 400)]
    [ProducesResponseType(typeof(ApiResult<object>), 401)]
    [ProducesResponseType(typeof(ApiResult<object>), 403)]
    [ProducesResponseType(typeof(ApiResult<object>), 404)]
    public async Task<IActionResult> DeleteSection([FromRoute] Guid sectionId)
    {
        await _portfolioService.DeleteSectionAsync(sectionId);
        return Ok(ApiResult.Success("200", "Portfolio section deleted successfully."));
    }

    [HttpPut("me/sections/reorder")]
    [Authorize(Roles = "Student")]
    [SwaggerOperation(
        Summary = "Reorder portfolio sections",
        Description = "Updates display order for the provided section ids.")]
    [ProducesResponseType(typeof(ApiResult<PortfolioResponseDto>), 200)]
    [ProducesResponseType(typeof(ApiResult<object>), 400)]
    [ProducesResponseType(typeof(ApiResult<object>), 401)]
    [ProducesResponseType(typeof(ApiResult<object>), 403)]
    [ProducesResponseType(typeof(ApiResult<object>), 404)]
    public async Task<IActionResult> ReorderSections(
        [FromBody, SwaggerParameter("Section display order updates")] ReorderPortfolioSectionsRequestDto dto)
    {
        var result = await _portfolioService.ReorderSectionsAsync(dto);
        return Ok(ApiResult<PortfolioResponseDto>.Success(
            result,
            "200",
            "Portfolio sections reordered successfully."));
    }

    [HttpPost("me/sync")]
    [Authorize(Roles = "Student")]
    [SwaggerOperation(
        Summary = "Sync auto-imported portfolio items",
        Description = "Idempotently imports certificates, graded capstone projects, and completed highlight reels.")]
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
        Description = "Anonymous endpoint serving the published snapshot (not the live draft).")]
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
