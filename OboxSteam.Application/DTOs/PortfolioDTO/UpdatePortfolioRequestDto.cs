using System.ComponentModel.DataAnnotations;

namespace OboxSteam.Application.DTOs.PortfolioDTO;

public class UpdatePortfolioRequestDto
{
    public bool? IsPublic { get; set; }

    [MaxLength(100)]
    public string? Subdomain { get; set; }

    [MaxLength(255)]
    public string? DisplayName { get; set; }

    [MaxLength(255)]
    public string? Headline { get; set; }

    [MaxLength(255)]
    public string? Tagline { get; set; }

    public string? Summary { get; set; }

    public ThemeConfigDto? Theme { get; set; }

    public List<PortfolioLinkDto>? Links { get; set; }
}
