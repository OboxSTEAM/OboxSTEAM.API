using System.ComponentModel.DataAnnotations;
using OboxSteam.Domain.Enums;

namespace OboxSteam.Domain.Entities;

/// <summary>
/// Media file owned by a portfolio (uploaded by the portfolio's student).
/// Separate from the class-scoped <see cref="MediaAsset"/> pipeline:
/// no face recognition or transcoding is involved.
/// </summary>
public class PortfolioMediaAsset : BaseEntity
{
    public Guid PortfolioId { get; set; }
    public Portfolio Portfolio { get; set; } = null!;

    public PortfolioMediaType Type { get; set; } = PortfolioMediaType.Image;

    [MaxLength(500)]
    public string Url { get; set; } = null!;

    [MaxLength(512)]
    public string S3Key { get; set; } = null!;

    [MaxLength(255)]
    public string FileName { get; set; } = null!;

    [MaxLength(100)]
    public string ContentType { get; set; } = null!;

    public long SizeBytes { get; set; }

    // Navigation
    public ICollection<PortfolioMediaPlacement> Placements { get; set; } =
        new List<PortfolioMediaPlacement>();
}
