using OboxSteam.Domain.Enums;

namespace OboxSteam.Application.DTOs.ProgramAdvisoryDTO;

public sealed class AdvisoryMineItemDto
{
    public Guid ProgramId { get; set; }

    public string Code { get; set; } = null!;

    public string Name { get; set; } = null!;

    public ProgramStatus Status { get; set; }

    public int? FrameworkVersionNumber { get; set; }

    /// <summary>True only when the caller is the program's responsible advisor.</summary>
    public bool IsAdvisor { get; set; }

    public DateTime? LatestActivityAt { get; set; }

    public int UnreadCount { get; set; }

    public int OpenPinCount { get; set; }

    public AdvisoryApprovalState ApprovalState { get; set; }
}
