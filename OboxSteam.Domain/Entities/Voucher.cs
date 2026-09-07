using System.ComponentModel.DataAnnotations;
using OboxSteam.Domain.Enums;

namespace OboxSteam.Domain.Entities;

/// <summary>
/// Discount code. Exactly one of <see cref="PercentOff"/> or <see cref="AmountOff"/> is set.
/// One code per payment via <see cref="Payment.VoucherId"/>. Usage counts are derived
/// from successful payments; disable a code with soft-delete.
/// </summary>
public class Voucher : BaseEntity
{
    [MaxLength(50)]
    public string Code { get; set; } = null!;

    /// <summary>Percent off after ownership deduction (e.g. 15 = 15%). Null when using amount off.</summary>
    public decimal? PercentOff { get; set; }

    /// <summary>Fixed amount off after ownership deduction. Null when using percent off.</summary>
    public decimal? AmountOff { get; set; }

    /// <summary>Null means the code does not expire.</summary>
    public DateTime? ExpiryAt { get; set; }

    /// <summary>Null means no platform-wide cap.</summary>
    public int? UsageLimit { get; set; }

    /// <summary>Null means no per-student cap.</summary>
    public int? MaxUsagePerStudent { get; set; }

    public VoucherScope Scope { get; set; }

    public ICollection<Payment> Payments { get; set; } = new List<Payment>();
}
