using OboxSteam.Application.DTOs.SkillDTO;
using OboxSteam.Domain.Enums;

namespace OboxSteam.Application.DTOs.MentorDTO;

public class MentorSkillDto
{
    public Guid Id { get; set; }
    public Guid MentorId { get; set; }
    public Guid SkillId { get; set; }
    public SkillSummaryDto Skill { get; set; } = null!;
    public SkillProficiencyLevel ProficiencyLevel { get; set; }
    public int YearsOfExperience { get; set; }
    public string? Description { get; set; }
    public string? Notes { get; set; }
    public bool IsPublic { get; set; }
    public List<MentorSkillEvidenceDto> Evidences { get; set; } = new();
    public DateTime CreatedAt { get; set; }
}
