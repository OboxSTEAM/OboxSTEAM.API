using OboxSteam.Domain.Entities;
using OboxSteam.Domain.Enums;

namespace OboxSteam.Application.Commons.CurriculumChanges;

/// <summary>
/// Whitelist of curriculum fields recorded in the change log. Fields outside this list
/// (price, status, framework, advisor, audit columns) never bump <c>CurriculumVersion</c>.
/// </summary>
public static class CurriculumChangeFieldCatalog
{
    public const string SkillFieldPrefix = "skill:";
    public const string ActivityLinkFieldPrefix = "activityLink:";
    public const string ActivityLinkRequiredFieldPrefix = "activityLinkRequired:";

    private static readonly Dictionary<Type, CurriculumEntityDescriptor> Descriptors = new()
    {
        [typeof(Program)] = new CurriculumEntityDescriptor(
            ProgramAdvisoryTargetType.Program,
            [
                Field(nameof(Program.Name), "Name", CurriculumFieldValueType.ShortText),
                Field(nameof(Program.Code), "Code", CurriculumFieldValueType.ShortText),
                Field(nameof(Program.SeriesName), "Series", CurriculumFieldValueType.ShortText),
                Field(nameof(Program.Description), "Description", CurriculumFieldValueType.LongText),
                Field(nameof(Program.Level), "Level", CurriculumFieldValueType.Enum),
                Field(nameof(Program.Category), "Category", CurriculumFieldValueType.Enum),
                Field(nameof(Program.EstimatedDuration), "Estimated duration", CurriculumFieldValueType.ShortText),
                Field(nameof(Program.SkillsGained), "Skills gained", CurriculumFieldValueType.LongText),
                Field(nameof(Program.ThumbnailUrl), "Thumbnail", CurriculumFieldValueType.Media),
            ],
            OrderProperty: null),
        [typeof(Module)] = new CurriculumEntityDescriptor(
            ProgramAdvisoryTargetType.Module,
            [
                Field(nameof(Module.Name), "Name", CurriculumFieldValueType.ShortText),
                Field(nameof(Module.Code), "Code", CurriculumFieldValueType.ShortText),
                Field(nameof(Module.ModuleType), "Module type", CurriculumFieldValueType.Enum),
                Field(nameof(Module.PrerequisiteModuleId), "Prerequisite module", CurriculumFieldValueType.ShortText),
                Field(nameof(Module.IsMandatory), "Mandatory", CurriculumFieldValueType.Boolean),
                Field(nameof(Module.LearningOutcomes), "Learning outcomes", CurriculumFieldValueType.List),
            ],
            nameof(Module.ModuleOrder)),
        [typeof(Course)] = new CurriculumEntityDescriptor(
            ProgramAdvisoryTargetType.Course,
            [
                Field(nameof(Course.Name), "Name", CurriculumFieldValueType.ShortText),
                Field(nameof(Course.Code), "Code", CurriculumFieldValueType.ShortText),
                Field(nameof(Course.Description), "Description", CurriculumFieldValueType.LongText),
            ],
            nameof(Course.CourseOrder)),
        [typeof(Activity)] = new CurriculumEntityDescriptor(
            ProgramAdvisoryTargetType.Activity,
            [
                Field(nameof(Activity.Name), "Name", CurriculumFieldValueType.ShortText),
                Field(nameof(Activity.Code), "Code", CurriculumFieldValueType.ShortText),
                Field(nameof(Activity.ActivityType), "Activity type", CurriculumFieldValueType.Enum),
                Field(nameof(Activity.Description), "Description", CurriculumFieldValueType.LongText),
                Field(nameof(Activity.DurationMinutes), "Duration", CurriculumFieldValueType.DurationMinutes),
                Field(nameof(Activity.RequireQrCheckin), "QR check-in required", CurriculumFieldValueType.Boolean),
                Field(nameof(Activity.RequireMediaEvidence), "Media evidence required", CurriculumFieldValueType.Boolean),
            ],
            nameof(Activity.ActivityOrder)),
        [typeof(Assignment)] = new CurriculumEntityDescriptor(
            ProgramAdvisoryTargetType.Assignment,
            [
                Field(nameof(Assignment.Title), "Title", CurriculumFieldValueType.ShortText),
                Field(nameof(Assignment.Code), "Code", CurriculumFieldValueType.ShortText),
                Field(nameof(Assignment.Description), "Description", CurriculumFieldValueType.LongText),
                Field(nameof(Assignment.AssignmentType), "Assignment type", CurriculumFieldValueType.Enum),
                Field(nameof(Assignment.MaxPoints), "Max points", CurriculumFieldValueType.Number),
                Field(nameof(Assignment.PassScore), "Pass score", CurriculumFieldValueType.Number),
                Field(nameof(Assignment.IsRequiredForModulePass), "Required for module pass", CurriculumFieldValueType.Boolean),
                Field(nameof(Assignment.AllowShuffle), "Shuffle questions", CurriculumFieldValueType.Boolean),
                Field(nameof(Assignment.QuestionBankId), "Question bank", CurriculumFieldValueType.ShortText),
                Field(nameof(Assignment.QuestionCount), "Question count", CurriculumFieldValueType.Number),
                Field(nameof(Assignment.ShuffleOptions), "Shuffle options", CurriculumFieldValueType.Boolean),
                Field(nameof(Assignment.EasyPercent), "Easy questions (%)", CurriculumFieldValueType.Number),
                Field(nameof(Assignment.MediumPercent), "Medium questions (%)", CurriculumFieldValueType.Number),
                Field(nameof(Assignment.HardPercent), "Hard questions (%)", CurriculumFieldValueType.Number),
                Field(nameof(Assignment.TimeLimitMinutes), "Time limit", CurriculumFieldValueType.DurationMinutes),
                Field(nameof(Assignment.MaxAttempts), "Max attempts", CurriculumFieldValueType.Number),
            ],
            OrderProperty: null),
        [typeof(ResearchMilestone)] = new CurriculumEntityDescriptor(
            ProgramAdvisoryTargetType.ResearchMilestone,
            [
                Field(nameof(ResearchMilestone.Title), "Title", CurriculumFieldValueType.ShortText),
                Field(nameof(ResearchMilestone.Code), "Code", CurriculumFieldValueType.ShortText),
                Field(nameof(ResearchMilestone.Description), "Description", CurriculumFieldValueType.LongText),
                Field(nameof(ResearchMilestone.IsCapstone), "Capstone", CurriculumFieldValueType.Boolean),
            ],
            nameof(ResearchMilestone.MilestoneOrder)),
        [typeof(Material)] = new CurriculumEntityDescriptor(
            ProgramAdvisoryTargetType.Material,
            [
                Field(nameof(Material.Title), "Title", CurriculumFieldValueType.ShortText),
                Field(nameof(Material.MaterialType), "Material type", CurriculumFieldValueType.Enum),
                Field(nameof(Material.FileUrl), "File", CurriculumFieldValueType.Media),
            ],
            OrderProperty: null),
    };

    private static readonly Dictionary<(ProgramAdvisoryTargetType, string), CurriculumFieldDefinition> ByFieldKey =
        Descriptors.Values
            .SelectMany(d => d.Fields.Select(f => (d.TargetType, Field: f)))
            .ToDictionary(x => (x.TargetType, x.Field.FieldKey), x => x.Field);

    /// <summary>
    /// True for entity types whose changes are captured: component entities plus the
    /// program-skill and milestone-activity link rows.
    /// </summary>
    public static bool IsTracked(Type entityType)
        => Descriptors.ContainsKey(entityType)
           || entityType == typeof(ProgramSkill)
           || entityType == typeof(ResearchMilestoneActivity);

    public static CurriculumEntityDescriptor? GetDescriptor(Type entityType)
        => Descriptors.GetValueOrDefault(entityType);

    public static (string? Label, CurriculumFieldValueType ValueType) Describe(
        ProgramAdvisoryTargetType targetType,
        string fieldKey)
    {
        if (ByFieldKey.TryGetValue((targetType, fieldKey), out var definition))
        {
            return (definition.Label, definition.ValueType);
        }

        if (fieldKey.StartsWith(SkillFieldPrefix, StringComparison.Ordinal)
            || fieldKey.StartsWith(ActivityLinkFieldPrefix, StringComparison.Ordinal)
            || fieldKey.StartsWith(ActivityLinkRequiredFieldPrefix, StringComparison.Ordinal))
        {
            return (null, CurriculumFieldValueType.Boolean);
        }

        return (null, CurriculumFieldValueType.ShortText);
    }

    private static CurriculumFieldDefinition Field(
        string propertyName,
        string label,
        CurriculumFieldValueType valueType)
        => new(propertyName, char.ToLowerInvariant(propertyName[0]) + propertyName[1..], label, valueType);
}
