using System.ComponentModel.DataAnnotations;

namespace OboxSteam.Application.DTOs.PortfolioDTO;

public class UpdatePortfolioSubdomainRequestDto
{
    [MaxLength(100)]
    public string? Subdomain { get; set; }
}
