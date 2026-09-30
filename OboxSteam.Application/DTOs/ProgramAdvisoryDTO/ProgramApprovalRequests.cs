using System.ComponentModel.DataAnnotations;

namespace OboxSteam.Application.DTOs.ProgramAdvisoryDTO;

public sealed class ApproveProgramRequest
{
    /// <summary>Curriculum version the advisor reviewed; must equal the current version.</summary>
    [Range(0, long.MaxValue)]
    public long CurriculumVersion { get; set; }

    [MaxLength(2000)]
    public string? Comment { get; set; }
}

public sealed class RevokeProgramApprovalRequest
{
    [MaxLength(2000)]
    public string? Reason { get; set; }
}
