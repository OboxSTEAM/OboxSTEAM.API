using OboxSteam.Application.DTOs.SkillDTO;

namespace OboxSteam.Application.DTOs.PortfolioDTO;

public class StudentSkillDto
{
    public Guid SkillId { get; set; }

    public SkillSummaryDto Skill { get; set; } = null!;

    public DateTime FirstAchievedAt { get; set; }

    public int EvidenceCount { get; set; }

    public List<SkillEvidenceDto> Evidences { get; set; } = [];
}
