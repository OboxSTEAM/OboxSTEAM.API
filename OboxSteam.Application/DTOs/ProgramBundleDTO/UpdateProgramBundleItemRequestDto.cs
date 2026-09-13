using System.ComponentModel.DataAnnotations;

namespace OboxSteam.Application.DTOs.ProgramBundleDTO;

public sealed class UpdateProgramBundleItemRequestDto
{
    public Guid? ProgramId { get; set; }

    [Range(1, int.MaxValue)]
    public int? SortOrder { get; set; }

    public bool? RequiresPreviousCompletion { get; set; }
}
