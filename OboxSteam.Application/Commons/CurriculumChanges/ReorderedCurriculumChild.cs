using OboxSteam.Domain.Enums;

namespace OboxSteam.Application.Commons.CurriculumChanges;

public sealed record ReorderedCurriculumChild(
    ProgramAdvisoryTargetType TargetType,
    Guid TargetId,
    string Label,
    int? FromOrder,
    int? ToOrder);
