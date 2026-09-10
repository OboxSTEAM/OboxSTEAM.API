using OboxSteam.Domain.Enums;

namespace OboxSteam.Application.DTOs.ProgramBundleDTO;

public sealed class ProgramBundleResponseDto
{
    public Guid Id { get; set; }

    public string Code { get; set; } = null!;

    public string Name { get; set; } = null!;

    public string? Description { get; set; }

    public string? ThumbnailUrl { get; set; }

    public ProgramCategory Category { get; set; }

    public Guid? FrameworkId { get; set; }

    public decimal Price { get; set; }

    /// <summary>Sum of item retail prices. Hint for the catalog discount vs list price.</summary>
    public decimal RetailTotal { get; set; }

    public ProgramBundleStatus Status { get; set; }

    public IReadOnlyList<ProgramBundleItemResponseDto> Items { get; set; } =
        Array.Empty<ProgramBundleItemResponseDto>();

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }
}
