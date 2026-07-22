using System.ComponentModel.DataAnnotations;
using OboxSteam.Domain.Enums;

namespace OboxSteam.Application.DTOs.MentorDTO;

public class CreateMentorSkillRequestDto
{
    [Required]
    public Guid SkillId { get; set; }

    public SkillProficiencyLevel ProficiencyLevel { get; set; } = SkillProficiencyLevel.Beginner;

    [MaxLength(500)]
    public string? Notes { get; set; }
}
