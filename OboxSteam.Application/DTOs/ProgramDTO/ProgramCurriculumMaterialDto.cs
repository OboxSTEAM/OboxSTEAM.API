using OboxSteam.Domain.Enums;

namespace OboxSteam.Application.DTOs.ProgramDTO;

public class ProgramCurriculumMaterialDto
{
    public Guid MaterialId { get; set; }

    public string MaterialName { get; set; } = null!;

    public MaterialType MaterialType { get; set; }
}
