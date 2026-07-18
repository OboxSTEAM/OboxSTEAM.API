using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using OboxSteam.Application.Utils;
using OboxSteam.Application.Validation;
using OboxSteam.Domain.Enums;

namespace OboxSteam.Application.DTOs.PortfolioDTO;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public class ThemeConfigDto
{
    [MaxLength(100)]
    public string? TemplateId { get; set; }

    [MaxLength(20)]
    public string? PrimaryColor { get; set; }

    [MaxLength(20)]
    public string? SecondaryColor { get; set; }

    [MaxLength(100)]
    public string? FontFamily { get; set; }

    [MaxLength(100)]
    public string? HeadingFontFamily { get; set; }

    [JsonConverter(typeof(CamelCaseJsonStringEnumConverter))]
    public PortfolioFontScale? FontScale { get; set; }

    [JsonConverter(typeof(CamelCaseJsonStringEnumConverter))]
    public PortfolioLineHeight? LineHeight { get; set; }

    [JsonConverter(typeof(CamelCaseJsonStringEnumConverter))]
    public PortfolioDensity? Density { get; set; }

    [MaxLength(20)]
    public string? AccentColor { get; set; }

    [JsonConverter(typeof(CamelCaseJsonStringEnumConverter))]
    public PortfolioBackgroundStyle? BackgroundStyle { get; set; }

    [MaxLength(500)]
    [Url]
    public string? BackgroundImageUrl { get; set; }

    [JsonConverter(typeof(CamelCaseJsonStringEnumConverter))]
    public PortfolioCardStyle? CardStyle { get; set; }

    [MaxLength(100)]
    public string? LayoutStyle { get; set; }

    /// <summary>
    /// FE-owned theme-override slot map (opaque to the backend except length + JSON-object shape).
    /// </summary>
    [MaxLength(2000)]
    [JsonObject]
    public string? SettingsJson { get; set; }

    /// <summary>Legacy section order; superseded by <c>sections</c> but kept for migration.</summary>
    public List<string> SectionOrder { get; set; } = [];
}
