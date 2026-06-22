using System.ComponentModel.DataAnnotations;

namespace OboxSteam.Application.DTOs.ResearchMilestoneDTO;

public class LinkMilestoneActivityRequestDto
{
    [Required(ErrorMessage = "ActivityId is required.")]
    public Guid ActivityId { get; set; }

    public bool IsRequiredForSubmission { get; set; }

    [Range(0, int.MaxValue, ErrorMessage = "DisplayOrder cannot be negative.")]
    public int DisplayOrder { get; set; }

    /// <summary>Required when the caller is a Mentor — scopes the operation to that cohort.</summary>
    public Guid? ClassId { get; set; }
}
