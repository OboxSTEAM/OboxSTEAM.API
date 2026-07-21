using System.ComponentModel.DataAnnotations;
using OboxSteam.Domain.Enums;

namespace OboxSteam.Domain.Entities;

/// <summary>
/// Links a mentor (<see cref="User"/>) to a catalog <see cref="Skill"/> with a proficiency level.
/// Used by managers when deciding among class-assignment requests.
/// </summary>
public class MentorSkill : BaseEntity
{
    public Guid MentorId { get; set; }
    public User Mentor { get; set; } = null!;

    public Guid SkillId { get; set; }
    public Skill Skill { get; set; } = null!;

    public SkillProficiencyLevel ProficiencyLevel { get; set; } = SkillProficiencyLevel.Beginner;

    [MaxLength(500)]
    public string? Notes { get; set; }
}
