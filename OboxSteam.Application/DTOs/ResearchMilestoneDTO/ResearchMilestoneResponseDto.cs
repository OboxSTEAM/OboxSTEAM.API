using OboxSteam.Application.DTOs.AssignmentDTO;

namespace OboxSteam.Application.DTOs.ResearchMilestoneDTO;

public class ResearchMilestoneResponseDto
{
    public Guid Id { get; set; }
    public string Code { get; set; } = null!;
    public Guid ModuleId { get; set; }
    public string Title { get; set; } = null!;
    public string? Description { get; set; }
    public int MilestoneOrder { get; set; }
    public bool IsCapstone { get; set; }
    public Guid AssignmentId { get; set; }
    public AssignmentResponseDto Assignment { get; set; } = null!;
    public List<ResearchMilestoneActivityResponseDto> Activities { get; set; } = [];
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
