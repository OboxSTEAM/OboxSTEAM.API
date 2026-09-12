using OboxSteam.Domain.Enums;

namespace OboxSteam.Application.DTOs.ProgramAdvisoryDTO;

public sealed class AdvisoryThreadDto
{
    public Guid Id { get; set; }

    public Guid ProgramId { get; set; }

    public Guid? SubmissionId { get; set; }

    public Guid AuthorUserId { get; set; }

    public string? AuthorName { get; set; }

    public ProgramAdvisoryTargetType TargetType { get; set; }

    public Guid? TargetId { get; set; }

    public string TargetLabel { get; set; } = null!;

    public string? TargetContext { get; set; }

    public ProgramAdvisoryThreadType Type { get; set; }

    public ProgramAdvisoryThreadStatus Status { get; set; }

    public ProgramAdvisoryAnchorKind? AnchorKind { get; set; }

    public string? AnchorField { get; set; }

    public string? AnchorQuote { get; set; }

    public string? LatestMessagePreview { get; set; }

    public DateTime LastMessageAt { get; set; }

    public DateTime CreatedAt { get; set; }

    public int MessageCount { get; set; }

    public Guid ConcurrencyVersion { get; set; }

    public long LatestActivitySequence { get; set; }

    public bool CanAddress { get; set; }

    public bool CanResolve { get; set; }

    public bool CanReopen { get; set; }

    public bool CanWaive { get; set; }

    public List<AdvisoryThreadEventDto> Events { get; set; } = [];
}
