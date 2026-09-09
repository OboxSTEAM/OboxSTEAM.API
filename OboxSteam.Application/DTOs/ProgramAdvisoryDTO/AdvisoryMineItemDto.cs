using OboxSteam.Domain.Enums;

namespace OboxSteam.Application.DTOs.ProgramAdvisoryDTO;

public sealed class AdvisoryMineItemDto
{
    public Guid ProgramId { get; set; }

    public string Code { get; set; } = null!;

    public string Name { get; set; } = null!;

    public bool IsAdvisor { get; set; }

    public int? FrameworkVersionNumber { get; set; }

    public ProgramStatus Status { get; set; }

    public DateTime? LatestActivityAt { get; set; }

    public string NextAction { get; set; } = "None";

    public int UnreadFeedbackCount { get; set; }
}
