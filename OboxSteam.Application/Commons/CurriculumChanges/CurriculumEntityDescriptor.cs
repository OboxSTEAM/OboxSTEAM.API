using OboxSteam.Domain.Enums;

namespace OboxSteam.Application.Commons.CurriculumChanges;

public sealed record CurriculumEntityDescriptor(
    ProgramAdvisoryTargetType TargetType,
    IReadOnlyList<CurriculumFieldDefinition> Fields,
    string? OrderProperty);
