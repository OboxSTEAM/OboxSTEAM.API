namespace OboxSteam.Application.DTOs.InvoiceDTO;

/// <summary>Response DTO for invoice detail queries.</summary>
public class InvoiceResponseDto
{
    public Guid Id { get; set; }
    public string InvoiceNumber { get; set; } = null!;

    public Guid PaymentId { get; set; }
    public string PaymentCode { get; set; } = null!;

    /// <summary>Null for bundle-only invoices.</summary>
    public Guid? ProgramId { get; set; }

    /// <summary>Set when the payment is a module retake / re-delivery fee.</summary>
    public Guid? ModuleId { get; set; }

    public Guid? BundleId { get; set; }

    /// <summary>Ownership deduction plus voucher. Zero when the payment has no discount.</summary>
    public decimal DiscountAmount { get; set; }

    public Guid IssuedToId { get; set; }
    public string BillingName { get; set; } = null!;
    public string BillingEmail { get; set; } = null!;

    public string ItemDescription { get; set; } = null!;

    public decimal SubTotal { get; set; }
    public decimal TotalAmount { get; set; }
    public string Currency { get; set; } = null!;

    public DateTime CreatedAt { get; set; }
}
