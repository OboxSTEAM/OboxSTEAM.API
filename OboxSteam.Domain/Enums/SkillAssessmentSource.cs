namespace OboxSteam.Domain.Enums;

/// <summary>
/// How a <see cref="Entities.StudentSkill"/> proficiency was last determined.
/// </summary>
public enum SkillAssessmentSource
{
    Manual,
    Llm,
    Mentor,
    System
}
