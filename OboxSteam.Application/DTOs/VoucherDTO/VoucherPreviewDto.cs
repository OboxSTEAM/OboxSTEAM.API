using OboxSteam.Domain.Enums;

namespace OboxSteam.Application.DTOs.VoucherDTO;

public sealed class VoucherPreviewDto
{
    public bool IsValid { get; set; }

    public string? ErrorCode { get; set; }

    public string? ErrorMessage { get; set; }

    public Guid? VoucherId { get; set; }

    public string? Code { get; set; }

    public VoucherScope? Scope { get; set; }

    public decimal? PercentOff { get; set; }

    public decimal? AmountOff { get; set; }

    /// <summary>Voucher slice only, after ownership deduction, clamped so final ≥ 0.</summary>
    public decimal DiscountAmount { get; set; }

    /// <summary>Amount after ownership deduction, before voucher.</summary>
    public decimal BaseAmount { get; set; }

    public decimal FinalAmount { get; set; }
}
