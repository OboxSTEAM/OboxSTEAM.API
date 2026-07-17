using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace OboxSteam.Application.DTOs.PortfolioDTO;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public class UpdatePortfolioRequestDto
{
    [MaxLength(255)]
    public string? DisplayName { get; set; }

    [MaxLength(255)]
    public string? Headline { get; set; }

    [MaxLength(255)]
    public string? Tagline { get; set; }

    public string? Summary { get; set; }

    [MaxLength(500)]
    [Url]
    public string? AvatarUrl { get; set; }

    [MaxLength(500)]
    [Url]
    public string? CoverImageUrl { get; set; }

    public ThemeConfigDto? Theme { get; set; }

    public List<PortfolioLinkDto>? Links { get; set; }
}
