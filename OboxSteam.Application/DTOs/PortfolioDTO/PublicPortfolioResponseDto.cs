namespace OboxSteam.Application.DTOs.PortfolioDTO;

public class PublicPortfolioResponseDto
{
    public string? Subdomain { get; set; }

    public string? DisplayName { get; set; }

    public string? Headline { get; set; }

    public string? Tagline { get; set; }

    public string? Summary { get; set; }

    public string? StudentName { get; set; }

    public string? AvatarUrl { get; set; }

    public string? CoverImageUrl { get; set; }

    public ThemeConfigDto? Theme { get; set; }

    public List<PortfolioLinkDto> Links { get; set; } = [];

    public List<PortfolioCustomItemResponseDto> Items { get; set; } = [];

    public List<PortfolioSectionResponseDto> Sections { get; set; } = [];
}
