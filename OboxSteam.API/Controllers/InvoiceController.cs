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
        Summary = "Get my invoices",
        Description = "Retrieve all invoices issued for the current user (student or parent), sorted by most recent.")]
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
        Summary = "Get invoice by ID",
        Description = "Returns the details of an invoice by its Invoice ID.")]
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
        Summary = "Get invoice by Payment ID",
        Description = "Returns the invoice created automatically after a successful payment. Returns 404 if the payment has not been confirmed.")]
    [ProducesResponseType(typeof(ApiResult<InvoiceResponseDto>), 200)]
    [ProducesResponseType(typeof(ApiResult<object>), 401)]
    [ProducesResponseType(typeof(ApiResult<object>), 404)]
    public async Task<IActionResult> GetByPaymentId([FromRoute] Guid paymentId)
    {
        var result = await _invoiceService.GetByPaymentId(paymentId);
        return Ok(ApiResult<InvoiceResponseDto>.Success(result, "200", "Invoice retrieved successfully."));
    }
}
