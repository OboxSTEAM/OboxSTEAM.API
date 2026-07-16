using System.ComponentModel.DataAnnotations;

namespace OboxSteam.Application.DTOs.PortfolioDTO;

public class PortfolioLinkDto
{
    [MaxLength(100)]
    public string Label { get; set; } = null!;

    [MaxLength(500)]
    public string Url { get; set; } = null!;
}
