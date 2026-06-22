using OboxSteam.Domain.Enums;

namespace OboxSteam.Application.DTOs.PortfolioDTO;

public class PortfolioResponseDto
{
    public Guid Id { get; set; }
    public string Code { get; set; } = null!;
    public Guid StudentId { get; set; }
    public string Subdomain { get; set; } = null!;
    public PlanType PlanType { get; set; }
    public bool IsPublic { get; set; }
    public List<PortfolioCustomItemResponseDto> Items { get; set; } = [];
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
