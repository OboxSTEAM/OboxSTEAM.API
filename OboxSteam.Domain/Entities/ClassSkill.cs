namespace OboxSteam.Domain.Entities;

/// <summary>
/// Required / desired skill tag on a <see cref="Class"/> cohort.
/// Informational match signal for the mentor board — not a hard gate on who may apply.
/// </summary>
public class ClassSkill : BaseEntity
{
    public Guid ClassId { get; set; }
    public Class Class { get; set; } = null!;

    public Guid SkillId { get; set; }
    public Skill Skill { get; set; } = null!;
}
