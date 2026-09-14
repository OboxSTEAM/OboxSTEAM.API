using OboxSteam.Domain.Enums;

namespace OboxSteam.Application.DTOs.VoucherDTO;

/// <summary>
/// Student-facing voucher catalog item. Omits usage history and manager-only caps.
/// </summary>
public sealed class VoucherAvailableDto
{
    public Guid Id { get; set; }

    public string Code { get; set; } = null!;

    public decimal? PercentOff { get; set; }

    public decimal? AmountOff { get; set; }

    public DateTime? StartsAt { get; set; }

    public DateTime? ExpiryAt { get; set; }

    public VoucherScope Scope { get; set; }
}
