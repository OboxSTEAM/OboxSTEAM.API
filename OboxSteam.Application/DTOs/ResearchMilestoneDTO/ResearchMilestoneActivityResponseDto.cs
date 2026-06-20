using OboxSteam.Domain.Enums;

namespace OboxSteam.Application.DTOs.ResearchMilestoneDTO;

public class ResearchMilestoneActivityResponseDto
{
    public Guid Id { get; set; }
    public Guid ActivityId { get; set; }
    public string ActivityCode { get; set; } = null!;
    public string ActivityTitle { get; set; } = null!;
    public ActivityType ActivityType { get; set; }
    public bool IsRequiredForSubmission { get; set; }
    public int DisplayOrder { get; set; }
}
