using System.ComponentModel.DataAnnotations;
using OboxSteam.Domain.Enums;

namespace OboxSteam.Domain.Entities;

/// <summary>
/// Shared STEAM skill catalog entry (Science / Technology / Engineering / Arts / Math / SoftSkill).
/// </summary>
public class Skill : BaseEntity
{
    [MaxLength(50)]
    public string Code { get; set; } = null!;

    [MaxLength(255)]
    public string Name { get; set; } = null!;

    public SkillCategory Category { get; set; }

    [MaxLength(100)]
    public string? Subcategory { get; set; }

    public string? Description { get; set; }

    // Navigation
    public ICollection<StudentSkill> StudentSkills { get; set; } = new List<StudentSkill>();
    public ICollection<MentorSkill> MentorSkills { get; set; } = new List<MentorSkill>();
    public ICollection<ClassSkill> ClassSkills { get; set; } = new List<ClassSkill>();
}
