using OboxSteam.Domain.Enums;

namespace OboxSteam.Application.DTOs.ModuleDTO;

public class UpdateModuleRequestDto
{
    public string? Code { get; set; }
    public Guid? ProgramId { get; set; }
    public string? Name { get; set; }
    public ModuleType? ModuleType { get; set; }
    public int? ModuleOrder { get; set; }
    public Guid? PrerequisiteModuleId { get; set; }
    public bool? IsMandatory { get; set; }
    public decimal? Price { get; set; }
    public decimal? RetakeFee { get; set; }
}
