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
                      + "Items are optional on create; publish requires at least two Active programs and a bundle price below the retail sum.")]
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
        Description = "Returns bundle list price, owned programs with deducted retail prices, and the amount after ownership. "
                      + "Optional voucherCode is applied after ownership; invalid codes return 200 with voucher.isValid=false. "
                      + "Students quote for themselves. Parent, Admin, and Manager must pass studentId.")]
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
