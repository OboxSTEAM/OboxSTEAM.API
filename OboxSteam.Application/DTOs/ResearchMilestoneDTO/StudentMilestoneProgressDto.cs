namespace OboxSteam.Application.DTOs.ResearchMilestoneDTO;

public class StudentMilestoneProgressDto
{
    public Guid ModuleEnrollmentId { get; set; }
    public Guid ModuleId { get; set; }
    public List<StudentMilestoneItemProgressDto> Milestones { get; set; } = [];
}
