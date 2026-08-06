using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace OboxSteam.Application.DTOs.PortfolioDTO;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public class ImportClassGalleryMediaRequestDto
{
    [Required]
    [MinLength(1)]
    public List<Guid> MediaAssetIds { get; set; } = [];

    /// <summary>When set, imported assets are appended to this portfolio item gallery.</summary>
    public Guid? PortfolioCustomItemId { get; set; }

    /// <summary>When set, imported assets are appended to this portfolio section gallery.</summary>
    public Guid? PortfolioSectionId { get; set; }
}

public class ImportClassGalleryMediaResponseDto
{
    public List<PortfolioMediaUploadResponseDto> Assets { get; set; } = [];

    public PortfolioCustomItemResponseDto? Item { get; set; }

    public PortfolioSectionResponseDto? Section { get; set; }
}
