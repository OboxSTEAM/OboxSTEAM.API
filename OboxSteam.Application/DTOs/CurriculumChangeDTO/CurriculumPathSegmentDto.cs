using OboxSteam.Domain.Enums;

namespace OboxSteam.Application.DTOs.CurriculumChangeDTO;

public sealed class CurriculumPathSegmentDto
{
    public ProgramAdvisoryTargetType TargetType { get; set; }
    public Guid TargetId { get; set; }
    public string Label { get; set; } = string.Empty;
}
