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

        var invoice = await _unitOfWork.Invoices.GetByIdAsync(
            invoiceId,
            i => i.Payment,
            i => i.Payment.ProgramEnrollment!,
            i => i.Payment.ModuleEnrollment!,
            i => i.Payment.ModuleEnrollment!.Module,
            i => i.Payment.BundleEnrollment!)
            ?? throw ErrorHelper.NotFound($"Invoice '{invoiceId}' not found.");

        EnsureCanView(currentUser, invoice.IssuedToId);
        return await MapToDtoAsync(invoice, invoice.Payment?.Code);
    }

    public async Task<InvoiceResponseDto> GetByPaymentId(Guid paymentId)
    {
        var currentUserId = _claimsService.GetCurrentUserId;
        var currentUser = await _unitOfWork.Users.GetByIdAsync(currentUserId)
            ?? throw ErrorHelper.Unauthorized("User not found.");

        var invoice = await _unitOfWork.Invoices.FirstOrDefaultAsync(
            i => i.PaymentId == paymentId && !i.IsDeleted,
            i => i.Payment,
            i => i.Payment.ProgramEnrollment!,
            i => i.Payment.ModuleEnrollment!,
            i => i.Payment.ModuleEnrollment!.Module,
            i => i.Payment.BundleEnrollment!)
            ?? throw ErrorHelper.NotFound($"Invoice for payment '{paymentId}' not found.");

        EnsureCanView(currentUser, invoice.IssuedToId);
        return await MapToDtoAsync(invoice, invoice.Payment?.Code);
    }

    public async Task<List<InvoiceResponseDto>> GetMyInvoices()
    {
        var userId = _claimsService.GetCurrentUserId;

        var invoices = await _unitOfWork.Invoices.GetAllAsync(
            i => i.IssuedToId == userId && !i.IsDeleted,
            i => i.Payment,
            i => i.Payment.ProgramEnrollment!,
            i => i.Payment.ModuleEnrollment!,
            i => i.Payment.ModuleEnrollment!.Module,
            i => i.Payment.BundleEnrollment!);

        var result = new List<InvoiceResponseDto>();
        foreach (var invoice in invoices.OrderByDescending(i => i.CreatedAt))
        {
            result.Add(await MapToDtoAsync(invoice, invoice.Payment?.Code));
        }

        return result;
    }

    private static void EnsureCanView(Domain.Entities.User currentUser, Guid issuedToId)
    {
        if (currentUser.Role != RoleType.Admin && currentUser.Role != RoleType.Manager)
        {
            if (issuedToId != currentUser.Id)
            {
                throw ErrorHelper.Forbidden("You do not have permission to view this invoice.");
            }
        }
    }

    private async Task<InvoiceResponseDto> MapToDtoAsync(
        Domain.Entities.Invoice invoice,
        string? paymentCode)
    {
        var payment = invoice.Payment;
        Guid? programId = payment?.ProgramEnrollment?.ProgramId;
        Guid? moduleId = payment?.ModuleEnrollment?.ModuleId;
        Guid? bundleId = payment?.BundleEnrollment?.BundleId;
        if (!bundleId.HasValue && payment?.BundleEnrollmentId.HasValue == true)
        {
            var bundleEnrollment = await _unitOfWork.BundleEnrollments.GetByIdAsync(
                payment.BundleEnrollmentId.Value);
            bundleId = bundleEnrollment?.BundleId;
        }

        if (!programId.HasValue && payment?.ModuleEnrollment?.Module != null)
        {
            programId = payment.ModuleEnrollment.Module.ProgramId;
        }

        if (!programId.HasValue && payment?.ModuleEnrollmentId.HasValue == true)
        {
            var moduleEnrollment = payment.ModuleEnrollment
                ?? await _unitOfWork.ModuleEnrollments.GetByIdAsync(payment.ModuleEnrollmentId.Value);
            if (moduleEnrollment != null)
            {
                moduleId ??= moduleEnrollment.ModuleId;
                if (moduleEnrollment.ProgramEnrollmentId.HasValue)
                {
                    var pe = await _unitOfWork.ProgramEnrollments.GetByIdAsync(
                        moduleEnrollment.ProgramEnrollmentId.Value);
                    programId = pe?.ProgramId;
                }

                if (!programId.HasValue)
                {
                    var module = await _unitOfWork.Modules.GetByIdAsync(moduleEnrollment.ModuleId);
                    programId = module?.ProgramId;
                }
            }
        }

        if (!programId.HasValue && !bundleId.HasValue)
        {
            throw ErrorHelper.NotFound(
                $"Program for invoice '{invoice.Id}' could not be resolved from payment.");
        }

        return new()
        {
            Id = invoice.Id,
            InvoiceNumber = invoice.InvoiceNumber,
            PaymentId = invoice.PaymentId,
            PaymentCode = paymentCode ?? string.Empty,
            ProgramId = programId,
            ModuleId = moduleId,
            BundleId = bundleId,
            DiscountAmount = payment?.DiscountAmount ?? 0m,
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
}
