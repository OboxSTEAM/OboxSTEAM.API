namespace OboxSteam.Application.DTOs.PaymentDTO;

/// <summary>Student asks a linked parent to pay for a bundle. No voucher on this flow.</summary>
public sealed class ParentBundlePaymentRequestDto
{
    public Guid BundleId { get; set; }

    public Guid ParentId { get; set; }
}
