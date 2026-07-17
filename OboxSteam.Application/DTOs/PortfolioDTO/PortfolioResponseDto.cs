using OboxSteam.Domain.Enums;

namespace OboxSteam.Application.DTOs.PortfolioDTO;

public class PortfolioResponseDto
{
    public Guid Id { get; set; }

    public string Code { get; set; } = null!;

    public Guid StudentId { get; set; }

    public string? StudentName { get; set; }

    public string? AvatarUrl { get; set; }

    public string? CoverImageUrl { get; set; }

    public string? Subdomain { get; set; }

    public string? DisplayName { get; set; }

    public string? Headline { get; set; }

    public string? Tagline { get; set; }

    public string? Summary { get; set; }

    public PlanType PlanType { get; set; }

    public bool IsPublic { get; set; }

    public DateTime? LastPublishedAt { get; set; }

    public bool HasUnpublishedChanges { get; set; }

    public ThemeConfigDto? Theme { get; set; }

    public List<PortfolioLinkDto> Links { get; set; } = [];

    public List<PortfolioCustomItemResponseDto> Items { get; set; } = [];

    public List<PortfolioSectionResponseDto> Sections { get; set; } = [];

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }
}
