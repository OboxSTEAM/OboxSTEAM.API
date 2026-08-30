using System.ComponentModel.DataAnnotations;

namespace OboxSteam.Application.DTOs.ResearchMilestoneDTO;

public class UpdateResearchMilestoneRequestDto
{
    [MaxLength(255)]
    public string? Title { get; set; }

    public string? Description { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "MilestoneOrder must be at least 1.")]
    public int? MilestoneOrder { get; set; }

    public bool? IsCapstone { get; set; }

    [MaxLength(255)]
    public string? AssignmentTitle { get; set; }

    public string? AssignmentDescription { get; set; }

    public int? MaxPoints { get; set; }

    public decimal? PassScore { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "TimeLimitMinutes must be at least 1.")]
    public int? TimeLimitMinutes { get; set; }
}
