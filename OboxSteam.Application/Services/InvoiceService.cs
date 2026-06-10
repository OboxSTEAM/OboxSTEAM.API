using OboxSteam.Application.DTOs.InvoiceDTO;
using OboxSteam.Application.Interfaces;
using OboxSteam.Application.Utils;
using OboxSteam.Domain.Enums;
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
        var currentUserId = _claimsService.GetCurrentUserId;
        var currentUser = await _unitOfWork.Users.GetByIdAsync(currentUserId)
            ?? throw ErrorHelper.Unauthorized("User not found.");

        var invoice = await _unitOfWork.Invoices.GetByIdAsync(invoiceId, i => i.Payment)
            ?? throw ErrorHelper.NotFound($"Invoice '{invoiceId}' not found.");

        // Check ownership: Admin/Manager can view all, otherwise must be IssuedTo
        if (currentUser.Role != RoleType.SuperAdmin && currentUser.Role != RoleType.Manager)
        {
            if (invoice.IssuedToId != currentUserId)
            {
                throw ErrorHelper.Forbidden("You do not have permission to view this invoice.");
            }
        }

        return MapToDto(invoice, invoice.Payment?.Code);
    }

    public async Task<InvoiceResponseDto> GetByPaymentId(Guid paymentId)
    {
        var currentUserId = _claimsService.GetCurrentUserId;
        var currentUser = await _unitOfWork.Users.GetByIdAsync(currentUserId)
            ?? throw ErrorHelper.Unauthorized("User not found.");

        var invoice = await _unitOfWork.Invoices.FirstOrDefaultAsync(
            i => i.PaymentId == paymentId && !i.IsDeleted,
            i => i.Payment)
            ?? throw ErrorHelper.NotFound($"Invoice for payment '{paymentId}' not found.");

        // Check ownership: Admin/Manager can view all, otherwise must be IssuedTo
        if (currentUser.Role != RoleType.SuperAdmin && currentUser.Role != RoleType.Manager)
        {
            if (invoice.IssuedToId != currentUserId)
            {
                throw ErrorHelper.Forbidden("You do not have permission to view this invoice.");
            }
        }

        return MapToDto(invoice, invoice.Payment?.Code);
    }

    public async Task<List<InvoiceResponseDto>> GetMyInvoices()
    {
        var userId = _claimsService.GetCurrentUserId;

        // Eager load Payment to prevent N+1 query issue
        var invoices = await _unitOfWork.Invoices.GetAllAsync(
            i => i.IssuedToId == userId && !i.IsDeleted,
            i => i.Payment);

        return invoices.OrderByDescending(i => i.CreatedAt)
            .Select(invoice => MapToDto(invoice, invoice.Payment?.Code))
            .ToList();
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
