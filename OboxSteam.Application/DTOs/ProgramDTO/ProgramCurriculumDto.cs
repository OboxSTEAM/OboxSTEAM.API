using OboxSteam.Application.DTOs.SkillDTO;

namespace OboxSteam.Application.DTOs.ProgramDTO;

public class ProgramCurriculumDto
{
    public Guid ProgramId { get; set; }

    public string ProgramName { get; set; } = null!;

    public List<SkillSummaryDto> Skills { get; set; } = new();

    public List<ProgramCurriculumModuleDto> Modules { get; set; } = new();
}
