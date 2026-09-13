using System.ComponentModel.DataAnnotations;
using OboxSteam.Domain.Enums;

namespace OboxSteam.Domain.Entities;

/// <summary>
/// Sellable multi-program pathway. Programs remain individually purchasable;
/// the bundle price is a discounted alternative to the sum of retail prices.
/// </summary>
public class ProgramBundle : BaseEntity
{
    [MaxLength(50)]
    public string Code { get; set; } = null!;

    [MaxLength(255)]
    public string Name { get; set; } = null!;

    public string? Description { get; set; }

    public string? ThumbnailUrl { get; set; }

    /// <summary>Catalog hint only; item programs are not required to match.</summary>
    public ProgramCategory Category { get; set; }

    /// <summary>Optional Slice 5 framework tie-in. Null is allowed.</summary>
    public Guid? FrameworkId { get; set; }
    public ProgramFramework? Framework { get; set; }

    /// <summary>
    /// Share of item retail total charged as <see cref="Price"/> (e.g. 85 = 85%).
    /// Must be greater than 0 and less than 100 so the bundle stays cheaper than buying retail.
    /// </summary>
    public decimal PricePercent { get; set; } = 85m;

    /// <summary>Persisted selling price: retail total × <see cref="PricePercent"/> / 100.</summary>
    public decimal Price { get; set; }

    public ProgramBundleStatus Status { get; set; } = ProgramBundleStatus.Draft;

    public ICollection<ProgramBundleItem> Items { get; set; } = new List<ProgramBundleItem>();
    public ICollection<BundleEnrollment> Enrollments { get; set; } = new List<BundleEnrollment>();
    public ICollection<Certificate> Certificates { get; set; } = new List<Certificate>();
}
