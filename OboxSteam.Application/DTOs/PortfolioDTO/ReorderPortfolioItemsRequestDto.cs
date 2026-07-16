using System.ComponentModel.DataAnnotations;

namespace OboxSteam.Application.DTOs.PortfolioDTO;

public class ReorderPortfolioItemsRequestDto
{
    [Required]
    public List<ReorderPortfolioItemEntryDto> Items { get; set; } = [];
}

public class ReorderPortfolioItemEntryDto
{
    public Guid Id { get; set; }

    [Range(0, int.MaxValue, ErrorMessage = "DisplayOrder cannot be negative.")]
    public int DisplayOrder { get; set; }
}
