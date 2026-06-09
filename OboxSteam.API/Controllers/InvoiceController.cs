using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OboxSteam.Application.DTOs.InvoiceDTO;
using OboxSteam.Application.Interfaces;
using OboxSteam.Application.Utils;
using Swashbuckle.AspNetCore.Annotations;

namespace OboxSteam.API.Controllers;

[Route("api/invoices")]
[ApiController]
[Authorize]
public class InvoiceController : ControllerBase
{
    private readonly IInvoiceService _invoiceService;

    public InvoiceController(IInvoiceService invoiceService)
    {
        _invoiceService = invoiceService;
    }

    // =========================================================================
    // GET /api/invoices/my
    // =========================================================================

    [HttpGet("my")]
    [SwaggerOperation(
        Summary = "Lấy danh sách invoice của tôi",
        Description = "Trả về tất cả invoice đã phát hành cho user hiện tại (student hoặc parent), sắp xếp mới nhất trước.")]
    [ProducesResponseType(typeof(ApiResult<List<InvoiceResponseDto>>), 200)]
    [ProducesResponseType(typeof(ApiResult<object>), 401)]
    public async Task<IActionResult> GetMyInvoices()
    {
        var result = await _invoiceService.GetMyInvoices();
        return Ok(ApiResult<List<InvoiceResponseDto>>.Success(result, "200", "Invoices retrieved successfully."));
    }

    // =========================================================================
    // GET /api/invoices/{id}
    // =========================================================================

    [HttpGet("{id:guid}")]
    [SwaggerOperation(
        Summary = "Lấy invoice theo ID",
        Description = "Trả về chi tiết một invoice theo Invoice ID.")]
    [ProducesResponseType(typeof(ApiResult<InvoiceResponseDto>), 200)]
    [ProducesResponseType(typeof(ApiResult<object>), 401)]
    [ProducesResponseType(typeof(ApiResult<object>), 404)]
    public async Task<IActionResult> GetById([FromRoute] Guid id)
    {
        var result = await _invoiceService.GetById(id);
        return Ok(ApiResult<InvoiceResponseDto>.Success(result, "200", "Invoice retrieved successfully."));
    }

    // =========================================================================
    // GET /api/invoices/by-payment/{paymentId}
    // =========================================================================

    [HttpGet("by-payment/{paymentId:guid}")]
    [SwaggerOperation(
        Summary = "Lấy invoice theo Payment ID",
        Description = "Trả về invoice được tạo tự động sau khi payment thành công. Trả 404 nếu payment chưa được xác nhận.")]
    [ProducesResponseType(typeof(ApiResult<InvoiceResponseDto>), 200)]
    [ProducesResponseType(typeof(ApiResult<object>), 401)]
    [ProducesResponseType(typeof(ApiResult<object>), 404)]
    public async Task<IActionResult> GetByPaymentId([FromRoute] Guid paymentId)
    {
        var result = await _invoiceService.GetByPaymentId(paymentId);
        return Ok(ApiResult<InvoiceResponseDto>.Success(result, "200", "Invoice retrieved successfully."));
    }
}
