using System.ComponentModel.DataAnnotations;

namespace OboxSteam.Application.DTOs.PortfolioDTO;

public class UpdatePortfolioSettingsRequestDto
{
    public bool? IsPublic { get; set; }

    [MaxLength(100)]
    public string? Subdomain { get; set; }
}
