namespace OboxSteam.Application.DTOs.CurriculumChangeDTO;

public sealed class CurriculumChangesDto
{
    public long FromVersion { get; set; }
    public long ToVersion { get; set; }
    public long CurrentVersion { get; set; }
    public long SeenVersion { get; set; }
    public CurriculumChangeSummaryDto Summary { get; set; } = new();
    public List<CurriculumChangeItemDto> Items { get; set; } = [];
}
