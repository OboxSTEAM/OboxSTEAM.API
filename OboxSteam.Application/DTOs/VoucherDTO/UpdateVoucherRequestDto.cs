using System.ComponentModel.DataAnnotations;
using OboxSteam.Domain.Enums;

namespace OboxSteam.Application.DTOs.VoucherDTO;

public sealed class UpdateVoucherRequestDto
{
    /// <summary>When the code becomes usable.</summary>
    public DateTime? StartsAt { get; set; }

    public DateTime? ExpiryAt { get; set; }

    /// <summary>When true, clears expiry. Ignored when <see cref="ExpiryAt"/> is set.</summary>
    public bool? ClearExpiryAt { get; set; }

    [Range(1, int.MaxValue)]
    public int? UsageLimit { get; set; }

    /// <summary>When true, clears the platform-wide cap. Ignored when <see cref="UsageLimit"/> is set.</summary>
    public bool? ClearUsageLimit { get; set; }

    [Range(1, int.MaxValue)]
    public int? MaxUsagePerStudent { get; set; }

    /// <summary>When true, clears the per-student cap. Ignored when <see cref="MaxUsagePerStudent"/> is set.</summary>
    public bool? ClearMaxUsagePerStudent { get; set; }

    public VoucherScope? Scope { get; set; }
}
