using OboxSteam.Domain.Enums;

namespace OboxSteam.Application.DTOs.SkillDTO;

public class SkillDetailDto
{
    public Guid Id { get; set; }

    public string Code { get; set; } = null!;

    public string Name { get; set; } = null!;

    public SkillCategory Category { get; set; }

    public string? Subcategory { get; set; }

    public string? Description { get; set; }
}
