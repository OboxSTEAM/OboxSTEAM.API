using System.ComponentModel.DataAnnotations;

namespace OboxSteam.Application.DTOs.PortfolioDTO;

public class UpdatePortfolioItemRequestDto
{
    [MaxLength(255)]
    public string? Title { get; set; }

    public string? Description { get; set; }

    public string? StudentEditedBody { get; set; }

    public string? MediaUrl { get; set; }

    [Range(0, int.MaxValue, ErrorMessage = "DisplayOrder cannot be negative.")]
    public int? DisplayOrder { get; set; }

    public bool? IsVisible { get; set; }
}
