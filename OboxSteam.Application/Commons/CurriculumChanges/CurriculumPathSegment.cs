using OboxSteam.Domain.Enums;

namespace OboxSteam.Application.Commons.CurriculumChanges;

public sealed record CurriculumPathSegment(ProgramAdvisoryTargetType TargetType, Guid TargetId, string Label);
