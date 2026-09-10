using OboxSteam.Application.DTOs.VoucherDTO;

namespace OboxSteam.Application.DTOs.ProgramBundleDTO;

/// <summary>
/// Purchase quote: bundle list price, per-program ownership deductions, then optional voucher.
/// </summary>
public sealed class BundlePriceQuoteDto
{
    public Guid BundleId { get; set; }

    public string BundleName { get; set; } = null!;

    /// <summary>Catalog bundle price before ownership or voucher.</summary>
    public decimal BundlePrice { get; set; }

    public IReadOnlyList<OwnedProgramDeductionDto> OwnedPrograms { get; set; } =
        Array.Empty<OwnedProgramDeductionDto>();

    /// <summary>Sum of <see cref="OwnedProgramDeductionDto.DeductedPrice"/> (may exceed bundle price).</summary>
    public decimal OwnershipDeduction { get; set; }

    /// <summary>clamp(bundlePrice − ownershipDeduction, 0), before voucher.</summary>
    public decimal PriceAfterOwnership { get; set; }

    /// <summary>Set when a voucher code was supplied. Invalid codes keep <see cref="FinalPrice"/> at ownership result.</summary>
    public VoucherPreviewDto? Voucher { get; set; }

    /// <summary>Amount to charge after ownership and voucher. Zero means activate without Stripe.</summary>
    public decimal FinalPrice { get; set; }
}
