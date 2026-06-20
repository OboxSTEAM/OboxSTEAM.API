namespace OboxSteam.Application.DTOs.ProgramDTO;

public class ProgramCurriculumMilestoneDto
{
    public Guid MilestoneId { get; set; }

    public string MilestoneName { get; set; } = null!;

    public int MilestoneOrder { get; set; }

    public List<ProgramCurriculumActivityDto> Activities { get; set; } = new();
}
