using System.ComponentModel.DataAnnotations;

namespace OboxSteam.Application.DTOs.ProgramBundleDTO;

public sealed class CreateProgramBundleItemRequestDto
{
    [Required]
    public Guid ProgramId { get; set; }

    /// <summary>When omitted, order follows the request list (1-based).</summary>
    [Range(1, int.MaxValue)]
    public int? SortOrder { get; set; }

    public bool RequiresPreviousCompletion { get; set; }
}
