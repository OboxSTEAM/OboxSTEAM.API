using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using OboxSteam.Application.Utils;
using OboxSteam.Domain.Enums;

namespace OboxSteam.Application.DTOs.PortfolioDTO;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public class UpdatePortfolioItemRequestDto
{
    [MaxLength(255)]
    public string? Title { get; set; }

    [MaxLength(255)]
    public string? Subtitle { get; set; }

    [MaxLength(255)]
    public string? Organization { get; set; }

    public DateTime? StartDate { get; set; }

    public DateTime? EndDate { get; set; }

    public string? Description { get; set; }

    public string? StudentEditedBody { get; set; }

    public string? MediaUrl { get; set; }

    public string? ExternalUrl { get; set; }

    [Range(0, int.MaxValue, ErrorMessage = "DisplayOrder cannot be negative.")]
    public int? DisplayOrder { get; set; }

    public bool? IsVisible { get; set; }

    [MaxLength(20)]
    public string? AccentColor { get; set; }

    public bool? IsFeatured { get; set; }

    [JsonConverter(typeof(CamelCaseJsonStringEnumConverter))]
    public PortfolioItemSpan? Span { get; set; }

    /// <summary>When provided, replaces the entire gallery (empty list clears).</summary>
    public List<PortfolioMediaAssetInputDto>? MediaAssets { get; set; }
}
