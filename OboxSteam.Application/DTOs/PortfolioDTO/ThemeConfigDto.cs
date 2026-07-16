namespace OboxSteam.Application.DTOs.PortfolioDTO;

public class ThemeConfigDto
{
    public string? TemplateId { get; set; }

    public string? PrimaryColor { get; set; }

    public string? SecondaryColor { get; set; }

    public string? FontFamily { get; set; }

    public string? LayoutStyle { get; set; }

    public List<string> SectionOrder { get; set; } = [];
}
