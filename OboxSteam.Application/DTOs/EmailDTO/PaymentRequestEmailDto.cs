namespace OboxSteam.Application.DTOs.EmailDTO;

/// <summary>Data needed to send the parent payment request email.</summary>
public class PaymentRequestEmailDto
{
    public string To { get; set; } = null!;
    public string ParentName { get; set; } = null!;
    public string StudentName { get; set; } = null!;
    public string ProgramName { get; set; } = null!;
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "VND";

    /// <summary>Full URL for the parent to open the checkout page.</summary>
    public string PaymentLink { get; set; } = null!;
}
