using System.ComponentModel.DataAnnotations;
using OboxSteam.Domain.Enums;

namespace OboxSteam.Application.DTOs.ProgramBundleDTO;

public sealed class UpdateProgramBundleRequestDto
{
    [Required]
    [MaxLength(255)]
    public string Name { get; set; } = null!;

    public string? Description { get; set; }

    public string? ThumbnailUrl { get; set; }

    public ProgramCategory Category { get; set; }

    /// <summary>Optional Slice 5 framework. Item programs are not required to match.</summary>
    public Guid? FrameworkId { get; set; }

    /// <summary>Percent of item retail total charged as the bundle price (e.g. 85).</summary>
    [Range(0.01, 99.99)]
    public decimal PricePercent { get; set; }
}
