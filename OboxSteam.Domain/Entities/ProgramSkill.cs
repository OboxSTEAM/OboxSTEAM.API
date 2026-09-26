namespace OboxSteam.Domain.Entities;

/// <summary>
/// Catalog skill taught by a program. <see cref="ModuleId"/> is optional and is not
/// set by the program skillIds API; a null module means the skill belongs to the whole program.
/// </summary>
public class ProgramSkill : BaseEntity
{
    public Guid ProgramId { get; set; }
    public Program Program { get; set; } = null!;

    public Guid SkillId { get; set; }
    public Skill Skill { get; set; } = null!;

    public Guid? ModuleId { get; set; }
    public Module? Module { get; set; }
}
