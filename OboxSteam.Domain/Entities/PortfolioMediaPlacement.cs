using System.ComponentModel.DataAnnotations;

namespace OboxSteam.Domain.Entities;

/// <summary>
/// Places a portfolio-owned media asset in a gallery. Exactly one of
/// <see cref="PortfolioCustomItemId"/> or <see cref="PortfolioSectionId"/>
/// is set (enforced in the application layer and by a DB check constraint).
/// </summary>
public class PortfolioMediaPlacement : BaseEntity
{
    public Guid PortfolioMediaAssetId { get; set; }
    public PortfolioMediaAsset MediaAsset { get; set; } = null!;

    public Guid? PortfolioCustomItemId { get; set; }
    public PortfolioCustomItem? PortfolioCustomItem { get; set; }

    public Guid? PortfolioSectionId { get; set; }
    public PortfolioSection? PortfolioSection { get; set; }

    [MaxLength(255)]
    public string? Caption { get; set; }

    public int DisplayOrder { get; set; }
}
