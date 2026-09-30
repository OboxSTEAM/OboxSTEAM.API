using OboxSteam.Application.DTOs.CurriculumChangeDTO;
using OboxSteam.Domain.Enums;

namespace OboxSteam.Application.DTOs.ProgramAdvisoryDTO;

public sealed class MentionTargetDto
{
    public ProgramAdvisoryTargetType TargetType { get; set; }
    public Guid TargetId { get; set; }
    public string Label { get; set; } = string.Empty;
    public string? Code { get; set; }

    /// <summary>Ancestors, program first.</summary>
    public List<CurriculumPathSegmentDto> Path { get; set; } = [];

    public Guid? ModuleId { get; set; }
    public Guid? CourseId { get; set; }

    /// <summary>Set for activities and materials.</summary>
    public Guid? ActivityId { get; set; }

    /// <summary>Zero-based index in curriculum tree order.</summary>
    public int Order { get; set; }
}
