using OboxSteam.Domain.Enums;

namespace OboxSteam.Application.DTOs.ModuleDTO;

public class CreateModuleRequestDto
{
    public string Code { get; set; } = null!;
    public Guid ProgramId { get; set; }
    public string Name { get; set; } = null!;
    public ModuleType ModuleType { get; set; }
    public int ModuleOrder { get; set; }
    public Guid? PrerequisiteModuleId { get; set; }
    public bool IsMandatory { get; set; } = true;
    public decimal Price { get; set; }
    public decimal RetakeFee { get; set; }
}
