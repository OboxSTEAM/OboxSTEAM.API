using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace OboxSteam.Application.DTOs.PortfolioDTO;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public class ImportHighlightReelMediaRequestDto
{
    [Required]
    public Guid HighlightVideoItemId { get; set; }

    /// <summary>Gallery section that receives the placement. Required.</summary>
    [Required]
    public Guid PortfolioSectionId { get; set; }

    /// <summary>Optional caption (max 255). Defaults to stack strength or "Highlight reel".</summary>
    [MaxLength(255)]
    public string? Caption { get; set; }
}
