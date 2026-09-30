namespace OboxSteam.Application.DTOs.CurriculumChangeDTO;

public sealed class CurriculumChangeMoveDto
{
    public string? FromParentLabel { get; set; }
    public string? ToParentLabel { get; set; }
    public int? FromOrder { get; set; }
    public int? ToOrder { get; set; }
}
