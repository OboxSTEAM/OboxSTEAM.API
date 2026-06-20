using System.ComponentModel.DataAnnotations;

namespace OboxSteam.Application.DTOs.ResearchMilestoneDTO;

public class UpdateMilestoneActivityLinkRequestDto
{
    public bool? IsRequiredForSubmission { get; set; }

    [Range(0, int.MaxValue, ErrorMessage = "DisplayOrder cannot be negative.")]
    public int? DisplayOrder { get; set; }
}
