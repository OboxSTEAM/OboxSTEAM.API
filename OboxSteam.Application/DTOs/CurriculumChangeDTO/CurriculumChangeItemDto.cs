using OboxSteam.Domain.Enums;

namespace OboxSteam.Application.DTOs.CurriculumChangeDTO;

public sealed class CurriculumChangeItemDto
{
    public ProgramAdvisoryTargetType TargetType { get; set; }
    public Guid TargetId { get; set; }
    public string Label { get; set; } = string.Empty;
    public List<CurriculumPathSegmentDto> Path { get; set; } = [];
    public CurriculumChangeKind ChangeKind { get; set; }
    public List<CurriculumChangeFieldDto> Fields { get; set; } = [];
    public CurriculumChangeMoveDto? Moved { get; set; }
    public List<CurriculumReorderedChildDto> ReorderedChildren { get; set; } = [];
    public List<CurriculumChangeActorDto> ChangedBy { get; set; } = [];
    public DateTime LastChangedAt { get; set; }
    public bool IsUnseen { get; set; }
}
