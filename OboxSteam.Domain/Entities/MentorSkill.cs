using System.ComponentModel.DataAnnotations;
using OboxSteam.Domain.Enums;

namespace OboxSteam.Domain.Entities;

/// <summary>
/// Links a mentor (<see cref="User"/>) to a catalog <see cref="Skill"/> with
/// structured expertise. Mentors manage their own rows; students see only
/// public skills on mentor profiles.
/// </summary>
public class MentorSkill : BaseEntity
{
    public Guid MentorId { get; set; }
    public User Mentor { get; set; } = null!;

    public Guid SkillId { get; set; }
    public Skill Skill { get; set; } = null!;

    public SkillProficiencyLevel ProficiencyLevel { get; set; } = SkillProficiencyLevel.Beginner;

    /// <summary>Years practicing this skill (0–60). Enforced in the application layer.</summary>
    public int YearsOfExperience { get; set; }

    /// <summary>What the mentor actually does with this skill.</summary>
    public string? Description { get; set; }

    [MaxLength(500)]
    public string? Notes { get; set; }

    /// <summary>When true, students can see this skill on the mentor profile. Default public.</summary>
    public bool IsPublic { get; set; } = true;

    public ICollection<MentorSkillEvidence> Evidences { get; set; } = new List<MentorSkillEvidence>();
}
