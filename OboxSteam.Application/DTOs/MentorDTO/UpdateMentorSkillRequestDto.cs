using System.ComponentModel.DataAnnotations;
using OboxSteam.Domain.Enums;

namespace OboxSteam.Application.DTOs.MentorDTO;

public class UpdateMentorSkillRequestDto
{
    public SkillProficiencyLevel ProficiencyLevel { get; set; } = SkillProficiencyLevel.Beginner;

    public int YearsOfExperience { get; set; }

    [MaxLength(4000)]
    public string? Description { get; set; }

    [MaxLength(500)]
    public string? Notes { get; set; }

    public bool IsPublic { get; set; } = true;

    /// <summary>
    /// When non-null, replaces all evidence rows for this skill (empty list clears evidence).
    /// When null, existing evidence is left unchanged.
    /// </summary>
    public List<MentorSkillEvidenceRequestDto>? Evidences { get; set; }
}
