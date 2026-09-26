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

    /// <summary>
    /// RichText and Embed body. Persisted through <c>PortfolioHtmlSanitizer</c>, so it is not
    /// stored unchanged: tags outside the whitelist are removed, and characters such as
    /// <c>&amp;</c> are HTML-encoded. Embed keeps the source URL as text and does not
    /// validate providers. SkillsGroup does not use this field.
    /// </summary>
    public string? ContentHtml { get; set; }

    /// <summary>JSON bag of kind-specific rendering settings.</summary>
    public string? SettingsJson { get; set; }

    // Navigation
    public ICollection<PortfolioMediaPlacement> MediaPlacements { get; set; } =
        new List<PortfolioMediaPlacement>();
}
