using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using OboxSteam.Domain.Enums;

namespace OboxSteam.Application.DTOs.PortfolioDTO;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public class CreatePortfolioSectionRequestDto
{
    [Required]
    public PortfolioSectionKind Kind { get; set; }

    [MaxLength(255)]
    public string? Title { get; set; }

    [Range(0, int.MaxValue, ErrorMessage = "DisplayOrder cannot be negative.")]
    public int? DisplayOrder { get; set; }

    public bool? IsVisible { get; set; }

    public string? ContentHtml { get; set; }

    public string? SettingsJson { get; set; }

    public List<PortfolioMediaAssetInputDto>? MediaAssets { get; set; }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public class UpdatePortfolioSectionRequestDto
{
    [MaxLength(255)]
    public string? Title { get; set; }

    [Range(0, int.MaxValue, ErrorMessage = "DisplayOrder cannot be negative.")]
    public int? DisplayOrder { get; set; }

    public bool? IsVisible { get; set; }

    public string? ContentHtml { get; set; }

    public string? SettingsJson { get; set; }

    public List<PortfolioMediaAssetInputDto>? MediaAssets { get; set; }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public class ReorderPortfolioSectionsRequestDto
{
    [Required]
    public List<ReorderPortfolioSectionEntryDto> Sections { get; set; } = [];
}

public class ReorderPortfolioSectionEntryDto
{
    public Guid Id { get; set; }

    [Range(0, int.MaxValue, ErrorMessage = "DisplayOrder cannot be negative.")]
    public int DisplayOrder { get; set; }
}

public class PortfolioSectionResponseDto
{
    public Guid Id { get; set; }

    public PortfolioSectionKind Kind { get; set; }

    public string? Title { get; set; }

    public int DisplayOrder { get; set; }

    public bool IsVisible { get; set; }

    public string? ContentHtml { get; set; }

    public string? SettingsJson { get; set; }

    public List<PortfolioMediaAssetResponseDto> MediaAssets { get; set; } = [];

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }
}
