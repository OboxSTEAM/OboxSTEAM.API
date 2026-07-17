using System.ComponentModel.DataAnnotations;
using OboxSteam.Domain.Enums;

namespace OboxSteam.Domain.Entities;

public class PortfolioSection : BaseEntity
{
    public Guid PortfolioId { get; set; }
    public Portfolio Portfolio { get; set; } = null!;

    public PortfolioSectionKind Kind { get; set; }

    [MaxLength(255)]
    public string? Title { get; set; }

    public int DisplayOrder { get; set; }

    public bool IsVisible { get; set; } = true;

    /// <summary>Sanitized HTML body for RichText/Embed blocks.</summary>
    public string? ContentHtml { get; set; }

    /// <summary>JSON bag of kind-specific rendering settings.</summary>
    public string? SettingsJson { get; set; }

    // Navigation
    public ICollection<PortfolioMediaPlacement> MediaPlacements { get; set; } =
        new List<PortfolioMediaPlacement>();
}
