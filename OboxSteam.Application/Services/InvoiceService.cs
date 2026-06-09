using OboxSteam.Application.DTOs.InvoiceDTO;
using OboxSteam.Application.Interfaces;
using OboxSteam.Application.Utils;
using OboxSteam.Domain.Interfaces;

namespace OboxSteam.Application.Services;

public class InvoiceService : IInvoiceService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClaimsService _claimsService;

    public InvoiceService(IUnitOfWork unitOfWork, IClaimsService claimsService)
    {
        _unitOfWork = unitOfWork;
        _claimsService = claimsService;
    }

    public async Task<InvoiceResponseDto> GetById(Guid invoiceId)
    {
        var invoice = await _unitOfWork.Invoices.GetByIdAsync(invoiceId)
            ?? throw ErrorHelper.NotFound($"Invoice '{invoiceId}' not found.");

        var payment = await _unitOfWork.Payments.GetByIdAsync(invoice.PaymentId);

        return MapToDto(invoice, payment?.Code);
    }

    public async Task<InvoiceResponseDto> GetByPaymentId(Guid paymentId)
    {
        var invoice = await _unitOfWork.Invoices.FirstOrDefaultAsync(
            i => i.PaymentId == paymentId && !i.IsDeleted)
            ?? throw ErrorHelper.NotFound($"Invoice for payment '{paymentId}' not found.");

        var payment = await _unitOfWork.Payments.GetByIdAsync(invoice.PaymentId);

        return MapToDto(invoice, payment?.Code);
    }

    public async Task<List<InvoiceResponseDto>> GetMyInvoices()
    {
        var userId = _claimsService.GetCurrentUserId;

        var invoices = await _unitOfWork.Invoices.GetAllAsync(
            i => i.IssuedToId == userId && !i.IsDeleted);

        var result = new List<InvoiceResponseDto>();
        foreach (var invoice in invoices.OrderByDescending(i => i.CreatedAt))
        {
            var payment = await _unitOfWork.Payments.GetByIdAsync(invoice.PaymentId);
            result.Add(MapToDto(invoice, payment?.Code));
        }

        return result;
    }

    private static InvoiceResponseDto MapToDto(
        Domain.Entities.Invoice invoice, string? paymentCode) => new()
    {
        Id = invoice.Id,
        InvoiceNumber = invoice.InvoiceNumber,
        PaymentId = invoice.PaymentId,
        PaymentCode = paymentCode ?? string.Empty,
        IssuedToId = invoice.IssuedToId,
        BillingName = invoice.BillingName,
        BillingEmail = invoice.BillingEmail,
        ItemDescription = invoice.ItemDescription,
        SubTotal = invoice.SubTotal,
        TotalAmount = invoice.TotalAmount,
        Currency = invoice.Currency,
        CreatedAt = invoice.CreatedAt
    };
}
