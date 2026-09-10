using System.ComponentModel.DataAnnotations;
using OboxSteam.Domain.Enums;

namespace OboxSteam.Application.DTOs.VoucherDTO;

public sealed class CreateVoucherRequestDto
{
    [Required]
    [MaxLength(50)]
    public string Code { get; set; } = null!;

    /// <summary>Percent off after ownership deduction (e.g. 15 = 15%). Null when using amount off.</summary>
    [Range(0.01, 100)]
    public decimal? PercentOff { get; set; }

    /// <summary>Fixed amount off after ownership deduction. Null when using percent off.</summary>
    public decimal? AmountOff { get; set; }

    /// <summary>When the code becomes usable. Null means effective immediately.</summary>
    public DateTime? StartsAt { get; set; }

    public DateTime? ExpiryAt { get; set; }

    [Range(1, int.MaxValue)]
    public int? UsageLimit { get; set; }

    [Range(1, int.MaxValue)]
    public int? MaxUsagePerStudent { get; set; }

    public VoucherScope Scope { get; set; }
}
