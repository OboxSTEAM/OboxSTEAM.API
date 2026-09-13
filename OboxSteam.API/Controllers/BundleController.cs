using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OboxSteam.Application.Commons;
using OboxSteam.Application.DTOs.ProgramBundleDTO;
using OboxSteam.Application.Interfaces;
using OboxSteam.Application.Utils;
using OboxSteam.Domain.Enums;
using Swashbuckle.AspNetCore.Annotations;

namespace OboxSteam.API.Controllers;

[Route("api/bundles")]
[ApiController]
[Authorize]
public sealed class BundleController : ControllerBase
{
    private readonly IProgramBundleService _bundleService;
    private readonly IClaimsService _claimsService;

    public BundleController(IProgramBundleService bundleService, IClaimsService claimsService)
    {
        _bundleService = bundleService;
        _claimsService = claimsService;
    }

    [HttpGet]
    [AllowAnonymous]
    [SwaggerOperation(
        Summary = "List bundles",
        Description = "Paginated catalog of bundles. Item programs are omitted; call GET /api/bundles/{id} for items. "
                      + "Search matches code or name. Optional status and category filters. Anonymous callers, Student, and Parent "
                      + "only see Active bundles. Admin and Manager may list any status.")]
    [ProducesResponseType(typeof(ApiResult<Pagination<ProgramBundleResponseDto>>), 200)]
    [ProducesResponseType(typeof(ApiResult<object>), 400)]
    public async Task<IActionResult> GetAllBundles(
        [FromQuery, SwaggerParameter(Description = "Search by code or name (optional)")] string? search = null,
        [FromQuery, SwaggerParameter(Description = "Filter by status: Draft, Active, Inactive (optional). Ignored for Student, Parent, and anonymous callers.")] ProgramBundleStatus? status = null,
        [FromQuery, SwaggerParameter(Description = "Filter by category (optional)")] ProgramCategory? category = null,
        [FromQuery, SwaggerParameter(Description = "Page number, starting from 1")] int page = 1,
        [FromQuery, SwaggerParameter(Description = "Number of items per page")] int pageSize = 10)
    {
        if (page < 1 || pageSize < 1)
            return BadRequest(ApiResult<object>.Failure("400", "Invalid pagination parameters."));

        var result = await _bundleService.GetAllBundles(search, status, category, page, pageSize);
        return Ok(ApiResult<Pagination<ProgramBundleResponseDto>>.Success(
            result, "200", "Bundles retrieved successfully."));
    }

    [HttpGet("me")]
    [Authorize(Roles = "Student,Parent,Admin,Manager")]
    [SwaggerOperation(
        Summary = "List my purchased pathways",
        Description = "Students see their own Active/Completed bundle enrollments. Parents see linked students. "
                      + "Admin and Manager see all. Each row is a roadmap: Locked / Available / InProgress / Completed nodes, "
                      + "overall ProgressPercent, and the pathway certificate when issued. PendingPayment is omitted.")]
    [ProducesResponseType(typeof(ApiResult<Pagination<MyBundlePathwayDto>>), 200)]
    [ProducesResponseType(typeof(ApiResult<object>), 400)]
    [ProducesResponseType(typeof(ApiResult<object>), 401)]
    [ProducesResponseType(typeof(ApiResult<object>), 403)]
    public async Task<IActionResult> GetMyPathways(
        [FromQuery, SwaggerParameter(Description = "Page number, starting from 1")] int page = 1,
        [FromQuery, SwaggerParameter(Description = "Number of items per page")] int pageSize = 10)
    {
        if (page < 1 || pageSize < 1)
            return BadRequest(ApiResult<object>.Failure("400", "Invalid pagination parameters."));

        var result = await _bundleService.GetMyPathways(page, pageSize);
        return Ok(ApiResult<Pagination<MyBundlePathwayDto>>.Success(
            result, "200", "Pathways retrieved successfully."));
    }

    [HttpGet("me/{bundleEnrollmentId:guid}")]
    [Authorize(Roles = "Student,Parent,Admin,Manager")]
    [SwaggerOperation(
        Summary = "Get one purchased pathway",
        Description = "Roadmap for a single BundleEnrollment. Same visibility as GET /api/bundles/me.")]
    [ProducesResponseType(typeof(ApiResult<MyBundlePathwayDto>), 200)]
    [ProducesResponseType(typeof(ApiResult<object>), 401)]
    [ProducesResponseType(typeof(ApiResult<object>), 403)]
    [ProducesResponseType(typeof(ApiResult<object>), 404)]
    public async Task<IActionResult> GetMyPathwayByEnrollmentId([FromRoute] Guid bundleEnrollmentId)
    {
        var result = await _bundleService.GetMyPathwayByEnrollmentId(bundleEnrollmentId);
        return Ok(ApiResult<MyBundlePathwayDto>.Success(result, "200", "Pathway retrieved successfully."));
    }

    [HttpGet("{id:guid}")]
    [AllowAnonymous]
    [SwaggerOperation(
        Summary = "Get a bundle",
        Description = "Public detail with ordered items, retail total, and publication status.")]
    [ProducesResponseType(typeof(ApiResult<ProgramBundleResponseDto>), 200)]
    [ProducesResponseType(typeof(ApiResult<object>), 404)]
    public async Task<IActionResult> GetBundleById([FromRoute] Guid id)
    {
        var result = await _bundleService.GetBundleById(id);
        return Ok(ApiResult<ProgramBundleResponseDto>.Success(result, "200", "Bundle retrieved successfully."));
    }

    [HttpPost]
    [Authorize(Roles = "Admin,Manager")]
    [SwaggerOperation(
        Summary = "Create a bundle",
        Description = "Creates a Draft pathway. It is not purchasable until POST /api/bundles/{id}/publish. "
                      + "PricePercent (e.g. 85) is applied to the sum of item retail prices to persist Price. "
                      + "Items are optional on create; use POST /api/bundles/{id}/items to add programs later.")]
    [ProducesResponseType(typeof(ApiResult<ProgramBundleResponseDto>), 201)]
    [ProducesResponseType(typeof(ApiResult<object>), 400)]
    [ProducesResponseType(typeof(ApiResult<object>), 401)]
    [ProducesResponseType(typeof(ApiResult<object>), 403)]
    [ProducesResponseType(typeof(ApiResult<object>), 409)]
    public async Task<IActionResult> CreateBundle([FromBody] CreateProgramBundleRequestDto request)
    {
        var result = await _bundleService.CreateBundle(request);
        return CreatedAtAction(
            nameof(GetBundleById),
            new { id = result.Id },
            ApiResult<ProgramBundleResponseDto>.Success(result, "201", "Bundle created as Draft."));
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = "Admin,Manager")]
    [SwaggerOperation(
        Summary = "Update a bundle",
        Description = "Updates name, description, category, framework, and PricePercent. "
                      + "Price is recalculated as retailTotal × PricePercent / 100. Allowed on Draft, Inactive, and Active.")]
    [ProducesResponseType(typeof(ApiResult<ProgramBundleResponseDto>), 200)]
    [ProducesResponseType(typeof(ApiResult<object>), 400)]
    [ProducesResponseType(typeof(ApiResult<object>), 401)]
    [ProducesResponseType(typeof(ApiResult<object>), 403)]
    [ProducesResponseType(typeof(ApiResult<object>), 404)]
    public async Task<IActionResult> UpdateBundle(
        [FromRoute] Guid id,
        [FromBody] UpdateProgramBundleRequestDto request)
    {
        var result = await _bundleService.UpdateBundle(id, request);
        return Ok(ApiResult<ProgramBundleResponseDto>.Success(result, "200", "Bundle updated successfully."));
    }

    [HttpPost("{id:guid}/items")]
    [Authorize(Roles = "Admin,Manager")]
    [SwaggerOperation(
        Summary = "Add a program to a bundle",
        Description = "Draft or Inactive only. Recalculates Price from PricePercent × new retail total.")]
    [ProducesResponseType(typeof(ApiResult<ProgramBundleResponseDto>), 200)]
    [ProducesResponseType(typeof(ApiResult<object>), 400)]
    [ProducesResponseType(typeof(ApiResult<object>), 401)]
    [ProducesResponseType(typeof(ApiResult<object>), 403)]
    [ProducesResponseType(typeof(ApiResult<object>), 404)]
    [ProducesResponseType(typeof(ApiResult<object>), 409)]
    public async Task<IActionResult> AddBundleItem(
        [FromRoute] Guid id,
        [FromBody] CreateProgramBundleItemRequestDto request)
    {
        var result = await _bundleService.AddBundleItem(id, request);
        return Ok(ApiResult<ProgramBundleResponseDto>.Success(result, "200", "Bundle item added."));
    }

    [HttpPut("{id:guid}/items/{itemId:guid}")]
    [Authorize(Roles = "Admin,Manager")]
    [SwaggerOperation(
        Summary = "Update a bundle item",
        Description = "Draft or Inactive only. Change program, sort order, or RequiresPreviousCompletion. "
                      + "Changing the program recalculates Price.")]
    [ProducesResponseType(typeof(ApiResult<ProgramBundleResponseDto>), 200)]
    [ProducesResponseType(typeof(ApiResult<object>), 400)]
    [ProducesResponseType(typeof(ApiResult<object>), 401)]
    [ProducesResponseType(typeof(ApiResult<object>), 403)]
    [ProducesResponseType(typeof(ApiResult<object>), 404)]
    [ProducesResponseType(typeof(ApiResult<object>), 409)]
    public async Task<IActionResult> UpdateBundleItem(
        [FromRoute] Guid id,
        [FromRoute] Guid itemId,
        [FromBody] UpdateProgramBundleItemRequestDto request)
    {
        var result = await _bundleService.UpdateBundleItem(id, itemId, request);
        return Ok(ApiResult<ProgramBundleResponseDto>.Success(result, "200", "Bundle item updated."));
    }

    [HttpDelete("{id:guid}/items/{itemId:guid}")]
    [Authorize(Roles = "Admin,Manager")]
    [SwaggerOperation(
        Summary = "Remove a program from a bundle",
        Description = "Draft or Inactive only. Soft-deletes the item and recalculates Price.")]
    [ProducesResponseType(typeof(ApiResult<ProgramBundleResponseDto>), 200)]
    [ProducesResponseType(typeof(ApiResult<object>), 401)]
    [ProducesResponseType(typeof(ApiResult<object>), 403)]
    [ProducesResponseType(typeof(ApiResult<object>), 404)]
    [ProducesResponseType(typeof(ApiResult<object>), 409)]
    public async Task<IActionResult> DeleteBundleItem(
        [FromRoute] Guid id,
        [FromRoute] Guid itemId)
    {
        var result = await _bundleService.DeleteBundleItem(id, itemId);
        return Ok(ApiResult<ProgramBundleResponseDto>.Success(result, "200", "Bundle item removed."));
    }

    [HttpPost("{id:guid}/publish")]
    [Authorize(Roles = "Admin,Manager")]
    [SwaggerOperation(
        Summary = "Publish a bundle",
        Description = "Manager approval: Draft → Active. Requires ≥ 2 Active programs, unique items, "
                      + "the first item not gated, and Price lower than the sum of item retail prices.")]
    [ProducesResponseType(typeof(ApiResult<ProgramBundleResponseDto>), 200)]
    [ProducesResponseType(typeof(ApiResult<object>), 400)]
    [ProducesResponseType(typeof(ApiResult<object>), 401)]
    [ProducesResponseType(typeof(ApiResult<object>), 403)]
    [ProducesResponseType(typeof(ApiResult<object>), 404)]
    [ProducesResponseType(typeof(ApiResult<object>), 409)]
    public async Task<IActionResult> PublishBundle([FromRoute] Guid id)
    {
        var result = await _bundleService.PublishBundle(id);
        return Ok(ApiResult<ProgramBundleResponseDto>.Success(result, "200", "Bundle published successfully."));
    }

    [HttpGet("{id:guid}/price-quote")]
    [Authorize(Roles = "Student,Parent,Admin,Manager")]
    [SwaggerOperation(
        Summary = "Get a bundle price quote",
        Description = "Returns bundle list price (retail × PricePercent), owned programs with deducted retail prices, "
                      + "and the amount the student pays. Ownership is subtracted from bundle.Price, then optional voucherCode. "
                      + "Invalid codes return 200 with voucher.isValid=false. Students quote for themselves. "
                      + "Parent, Admin, and Manager must pass studentId.")]
    [ProducesResponseType(typeof(ApiResult<BundlePriceQuoteDto>), 200)]
    [ProducesResponseType(typeof(ApiResult<object>), 400)]
    [ProducesResponseType(typeof(ApiResult<object>), 401)]
    [ProducesResponseType(typeof(ApiResult<object>), 403)]
    [ProducesResponseType(typeof(ApiResult<object>), 404)]
    public async Task<IActionResult> GetPriceQuote(
        [FromRoute] Guid id,
        [FromQuery, SwaggerParameter(Description = "Learner to price for. Required for Parent, Admin, and Manager.")] Guid? studentId = null,
        [FromQuery, SwaggerParameter(Description = "Optional voucher code applied after ownership deduction.")] string? voucherCode = null)
    {
        var targetStudentId = studentId ?? _claimsService.GetCurrentUserId;
        var result = await _bundleService.GetPriceQuote(id, targetStudentId, voucherCode);
        return Ok(ApiResult<BundlePriceQuoteDto>.Success(result, "200", "Bundle price quote computed."));
    }
}
