using OboxSteam.Domain.Enums;

namespace OboxSteam.Application.DTOs.ResearchMilestoneDTO;

public class StudentMilestoneActivityProgressDto
{
    public Guid ActivityId { get; set; }
    public string Title { get; set; } = null!;
    public ActivityType ActivityType { get; set; }
    public bool IsRequiredForSubmission { get; set; }
    public bool IsSatisfied { get; set; }
}
