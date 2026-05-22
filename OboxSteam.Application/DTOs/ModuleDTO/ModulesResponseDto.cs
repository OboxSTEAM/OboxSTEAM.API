using OboxSteam.Application.DTOs.CourseDTO;
using OboxSteam.Domain.Enums;

namespace OboxSteam.Application.DTOs.ModuleDTO;

public class ModulesResponseDto
{
    public Guid Id { get; set; }
    public string Code { get; set; } = null!;
    public Guid ProgramId { get; set; }
    public string Name { get; set; } = null!;
    public ModuleType ModuleType { get; set; }
    public int ModuleOrder { get; set; }
    public Guid? PrerequisiteModuleId { get; set; }
    public bool IsMandatory { get; set; }
    public decimal Price { get; set; }
    public decimal RetakeFee { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public List<CourseResponseDto> Courses { get; set; } = new();
}
