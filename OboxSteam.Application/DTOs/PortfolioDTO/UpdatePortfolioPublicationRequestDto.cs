using System.ComponentModel.DataAnnotations;

namespace OboxSteam.Application.DTOs.PortfolioDTO;

public class UpdatePortfolioPublicationRequestDto
{
    [Required]
    public bool? IsPublished { get; set; }
}
