using OboxSteam.Domain.Enums;

namespace OboxSteam.Application.DTOs.ProgramDTO;

public class ProgramCurriculumActivityDto
{
    public Guid ActivityId { get; set; }

    public string ActivityName { get; set; } = null!;

    public int ActivityOrder { get; set; }

    public ActivityType ActivityType { get; set; }

    public ProgramCurriculumMaterialDto? Material { get; set; }
}
