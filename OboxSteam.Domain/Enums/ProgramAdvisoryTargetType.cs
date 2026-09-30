namespace OboxSteam.Domain.Enums;

public enum ProgramAdvisoryTargetType
{
    Program,
    Module,
    Course,
    Activity,
    Assignment,
    ResearchMilestone,
    Material,

    /// <summary>
    /// Rubric storage is dropped. Kept only so legacy advisory thread rows still
    /// materialize until the discussion data migration rewrites them.
    /// </summary>
    [Obsolete("Rubric criteria were removed; not a valid target.")]
    RubricCriterion,
}
