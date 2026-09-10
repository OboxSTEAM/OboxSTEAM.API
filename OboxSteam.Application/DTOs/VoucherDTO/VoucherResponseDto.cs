using OboxSteam.Domain.Enums;

namespace OboxSteam.Application.DTOs.VoucherDTO;

public sealed class VoucherResponseDto
{
    public Guid Id { get; set; }

    public string Code { get; set; } = null!;

    public decimal? PercentOff { get; set; }

    public decimal? AmountOff { get; set; }

    public DateTime? StartsAt { get; set; }

    public DateTime? ExpiryAt { get; set; }

    public int? UsageLimit { get; set; }

    public int? MaxUsagePerStudent { get; set; }

    public VoucherScope Scope { get; set; }

    public VoucherStatus Status { get; set; }

    /// <summary>Successful payments that used this code.</summary>
    public int UsageCount { get; set; }

    /// <summary>Null when <see cref="UsageLimit"/> is unlimited.</summary>
    public int? RemainingUses { get; set; }

    public bool IsExpired { get; set; }

    public bool IsNotYetActive { get; set; }

    public List<VoucherUsageItemDto> Usages { get; set; } = [];

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }
}
