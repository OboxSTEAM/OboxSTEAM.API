using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OboxSteam.Application.Commons;
using OboxSteam.Application.DTOs.VoucherDTO;
using OboxSteam.Application.Interfaces;
using OboxSteam.Application.Utils;
using OboxSteam.Domain.Enums;
using Swashbuckle.AspNetCore.Annotations;

namespace OboxSteam.API.Controllers;

[Route("api/vouchers")]
[ApiController]
[Authorize]
public sealed class VoucherController : ControllerBase
{
    private readonly IVoucherService _voucherService;
    private readonly IClaimsService _claimsService;

    public VoucherController(IVoucherService voucherService, IClaimsService claimsService)
    {
        _voucherService = voucherService;
        _claimsService = claimsService;
    }

    [HttpGet]
    [Authorize(Roles = "Admin,Manager")]
    [SwaggerOperation(
        Summary = "List vouchers",
        Description = "Paginated manager catalog. Search matches code. Usage history is omitted; use GET by id for payment rows.")]
    [ProducesResponseType(typeof(ApiResult<Pagination<VoucherResponseDto>>), 200)]
    [ProducesResponseType(typeof(ApiResult<object>), 400)]
    [ProducesResponseType(typeof(ApiResult<object>), 401)]
    [ProducesResponseType(typeof(ApiResult<object>), 403)]
    public async Task<IActionResult> GetAllVouchers(
        [FromQuery, SwaggerParameter(Description = "Search by code (optional)")] string? search = null,
        [FromQuery, SwaggerParameter(Description = "Filter by scope: Bundle, Program, Both (optional)")] VoucherScope? scope = null,
        [FromQuery, SwaggerParameter(Description = "Filter by status: Draft, Active (optional)")] VoucherStatus? status = null,
        [FromQuery, SwaggerParameter(Description = "Page number, starting from 1")] int page = 1,
        [FromQuery, SwaggerParameter(Description = "Number of items per page")] int pageSize = 10)
    {
        if (page < 1 || pageSize < 1)
            return BadRequest(ApiResult<object>.Failure("400", "Invalid pagination parameters."));

        var result = await _voucherService.GetAllVouchers(search, scope, status, page, pageSize);
        return Ok(ApiResult<Pagination<VoucherResponseDto>>.Success(
            result, "200", "Vouchers retrieved successfully."));
    }

    [HttpGet("{voucherId:guid}")]
    [Authorize(Roles = "Admin,Manager")]
    [SwaggerOperation(
        Summary = "Get voucher by ID",
        Description = "Manager detail including successful-payment usage history.")]
    [ProducesResponseType(typeof(ApiResult<VoucherResponseDto>), 200)]
    [ProducesResponseType(typeof(ApiResult<object>), 401)]
    [ProducesResponseType(typeof(ApiResult<object>), 403)]
    [ProducesResponseType(typeof(ApiResult<object>), 404)]
    public async Task<IActionResult> GetVoucherById([FromRoute] Guid voucherId)
    {
        var result = await _voucherService.GetVoucherById(voucherId);
        return Ok(ApiResult<VoucherResponseDto>.Success(result, "200", "Voucher retrieved successfully."));
    }

    [HttpPost]
    [Authorize(Roles = "Admin,Manager")]
    [SwaggerOperation(
        Summary = "Issue a voucher",
        Description = "Creates a discount code. Exactly one of PercentOff or AmountOff is required. Code is unique. Optional StartsAt delays when the code becomes usable (Draft until then, then Active).")]
    [ProducesResponseType(typeof(ApiResult<VoucherResponseDto>), 201)]
    [ProducesResponseType(typeof(ApiResult<object>), 400)]
    [ProducesResponseType(typeof(ApiResult<object>), 401)]
    [ProducesResponseType(typeof(ApiResult<object>), 403)]
    [ProducesResponseType(typeof(ApiResult<object>), 409)]
    public async Task<IActionResult> CreateVoucher([FromBody] CreateVoucherRequestDto request)
    {
        var result = await _voucherService.CreateVoucher(request);
        return CreatedAtAction(
            nameof(GetVoucherById),
            new { voucherId = result.Id },
            ApiResult<VoucherResponseDto>.Success(result, "201", "Voucher created successfully."));
    }

    [HttpPut("{voucherId:guid}")]
    [Authorize(Roles = "Admin,Manager")]
    [SwaggerOperation(
        Summary = "Update a voucher",
        Description = "Updates start date, expiry, usage caps, and scope. Code and discount type/amount cannot be changed.")]
    [ProducesResponseType(typeof(ApiResult<VoucherResponseDto>), 200)]
    [ProducesResponseType(typeof(ApiResult<object>), 400)]
    [ProducesResponseType(typeof(ApiResult<object>), 401)]
    [ProducesResponseType(typeof(ApiResult<object>), 403)]
    [ProducesResponseType(typeof(ApiResult<object>), 404)]
    [ProducesResponseType(typeof(ApiResult<object>), 409)]
    public async Task<IActionResult> UpdateVoucher(
        [FromRoute] Guid voucherId,
        [FromBody] UpdateVoucherRequestDto request)
    {
        var result = await _voucherService.UpdateVoucher(voucherId, request);
        return Ok(ApiResult<VoucherResponseDto>.Success(result, "200", "Voucher updated successfully."));
    }

    [HttpDelete("{voucherId:guid}")]
    [Authorize(Roles = "Admin,Manager")]
    [SwaggerOperation(
        Summary = "Disable a voucher",
        Description = "Soft-deletes the code so it can no longer be applied. Existing successful payments keep their voucher link.")]
    [ProducesResponseType(typeof(ApiResult<bool>), 200)]
    [ProducesResponseType(typeof(ApiResult<object>), 401)]
    [ProducesResponseType(typeof(ApiResult<object>), 403)]
    [ProducesResponseType(typeof(ApiResult<object>), 404)]
    public async Task<IActionResult> DeleteVoucher([FromRoute] Guid voucherId)
    {
        var result = await _voucherService.DeleteVoucher(voucherId);
        if (!result)
            return NotFound(ApiResult<object>.Failure("404", "Voucher not found."));

        return Ok(ApiResult<bool>.Success(result, "200", "Voucher disabled successfully."));
    }

    [HttpPost("preview")]
    [Authorize(Roles = "Student,Parent,Admin,Manager")]
    [SwaggerOperation(
        Summary = "Preview a voucher",
        Description = "Computes the voucher slice after ownership deduction. Invalid codes return 200 with isValid=false. "
                      + "Students preview for themselves. Parent, Admin, and Manager must pass studentId.")]
    [ProducesResponseType(typeof(ApiResult<VoucherPreviewDto>), 200)]
    [ProducesResponseType(typeof(ApiResult<object>), 400)]
    [ProducesResponseType(typeof(ApiResult<object>), 401)]
    [ProducesResponseType(typeof(ApiResult<object>), 403)]
    [ProducesResponseType(typeof(ApiResult<object>), 404)]
    public async Task<IActionResult> PreviewVoucher(
        [FromBody] PreviewVoucherRequestDto request,
        [FromQuery, SwaggerParameter(Description = "Learner to price for. Required for Parent, Admin, and Manager.")] Guid? studentId = null)
    {
        var targetStudentId = studentId ?? _claimsService.GetCurrentUserId;
        var result = await _voucherService.PreviewVoucher(targetStudentId, request);
        return Ok(ApiResult<VoucherPreviewDto>.Success(result, "200", "Voucher preview computed."));
    }
}
