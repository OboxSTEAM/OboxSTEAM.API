using System.ComponentModel.DataAnnotations;

namespace OboxSteam.Domain.Entities;

public class StudentSkill : BaseEntity
{
    public Guid StudentId { get; set; }
    public User Student { get; set; } = null!;

    [MaxLength(255)]
    public string SkillName { get; set; } = null!;

    [MaxLength(50)]
    public string SkillType { get; set; } = null!; // HardSkill, SoftSkill

    public int ProficiencyLevel { get; set; } // 1-100
}
