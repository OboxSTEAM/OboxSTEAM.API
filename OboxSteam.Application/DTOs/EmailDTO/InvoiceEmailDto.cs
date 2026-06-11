namespace OboxSteam.Application.DTOs.EmailDTO;

/// <summary>Data needed to send the invoice/receipt email after successful payment.</summary>
public class InvoiceEmailDto
{
    public string To { get; set; } = null!;
    public string PayerName { get; set; } = null!;
    public string StudentName { get; set; } = null!;
    public string ProgramName { get; set; } = null!;
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "VND";
    public string TransactionId { get; set; } = null!;
    public DateTime PaidAt { get; set; }

    /// <summary>Invoice code (same as Payment.Code, e.g. INV-26001).</summary>
    public string InvoiceCode { get; set; } = null!;

    /// <summary>The program thumbnail URL to display in the invoice.</summary>
    public string? ThumbnailUrl { get; set; }
}
