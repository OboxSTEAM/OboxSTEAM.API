using OboxSteam.Domain.Enums;

namespace OboxSteam.Application.Commons.CurriculumChanges;

public sealed record CurriculumFieldDefinition(
    string PropertyName,
    string FieldKey,
    string Label,
    CurriculumFieldValueType ValueType);
