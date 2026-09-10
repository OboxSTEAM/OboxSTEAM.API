namespace OboxSteam.Application.DTOs.VoucherDTO;

/// <summary>
/// One successful payment that used a voucher.
/// <see cref="DiscountAmount"/> is the combined ownership-plus-voucher value stored on Payment.
/// </summary>
public sealed class VoucherUsageItemDto
{
    public Guid PaymentId { get; set; }

    public string PaymentCode { get; set; } = null!;

    public Guid StudentId { get; set; }

    public string? StudentName { get; set; }

    public decimal Amount { get; set; }

    public decimal DiscountAmount { get; set; }

    public string Currency { get; set; } = null!;

    public DateTime PaidAt { get; set; }
}
