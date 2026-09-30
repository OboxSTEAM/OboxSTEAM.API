namespace OboxSteam.Application.DTOs.CurriculumChangeDTO;

public sealed class CurriculumChangeSummaryDto
{
    public int Created { get; set; }
    public int Updated { get; set; }
    public int Deleted { get; set; }

    /// <summary>Moved plus Reordered items.</summary>
    public int Moved { get; set; }
}
