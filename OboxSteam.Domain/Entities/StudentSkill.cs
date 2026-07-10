using OboxSteam.Domain.Enums;

namespace OboxSteam.Domain.Entities;

/// <summary>
/// Current proficiency snapshot linking a student to a catalog <see cref="Skill"/>.
/// Evidence rows live on <see cref="StudentSkillEvidence"/>.
/// </summary>
public class StudentSkill : BaseEntity
{
    public Guid StudentId { get; set; }
    public User Student { get; set; } = null!;

    public Guid SkillId { get; set; }
    public Skill Skill { get; set; } = null!;

    public SkillProficiencyLevel ProficiencyLevel { get; set; } = SkillProficiencyLevel.Beginner;

    public SkillAssessmentSource Source { get; set; } = SkillAssessmentSource.Manual;

    /// <summary>Model or gate confidence in [0, 1] when Source is Llm/System.</summary>
    public decimal? ConfidenceScore { get; set; }

    /// <summary>When proficiency was last assessed. First recorded time is <see cref="BaseEntity.CreatedAt"/>.</summary>
    public DateTime? LastAssessedAt { get; set; }

    public Guid? VerifiedBy { get; set; }
    public User? Verifier { get; set; }

    public DateTime? VerifiedAt { get; set; }

    public string? EvidenceSummary { get; set; }

    public string? Reasoning { get; set; }

    // Navigation
    public ICollection<StudentSkillEvidence> Evidences { get; set; } = new List<StudentSkillEvidence>();
}
