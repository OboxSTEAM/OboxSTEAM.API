using OboxSteam.Domain.Enums;

namespace OboxSteam.Application.DTOs.SkillDTO;

public class CreateSkillRequestDto
{
    public string? Code { get; set; }

    public string? Name { get; set; }

    public SkillCategory? Category { get; set; }

    public string? Subcategory { get; set; }

    public string? Description { get; set; }
}
