using OboxSteam.Domain.Enums;

namespace OboxSteam.Application.DTOs.ProgramAdvisoryDTO;

public sealed class AdvisoryReferenceDto
{
    public Guid Id { get; set; }
    public Guid ProgramId { get; set; }
    public AdvisoryReferenceContext Context { get; set; }
    public Guid? SubmissionId { get; set; }
    public ProgramAdvisoryTargetType TargetType { get; set; }
    public Guid TargetId { get; set; }
    public ProgramAdvisoryAnchorKind AnchorKind { get; set; }
    public string? FieldKey { get; set; }
    public string? Quote { get; set; }
    public string? QuotePrefix { get; set; }
    public string? QuoteSuffix { get; set; }
    public string CapturedLabel { get; set; } = null!;
    public string? CapturedExcerpt { get; set; }
    public DateTime CapturedAt { get; set; }
    public bool IsAvailable { get; set; }
    public string? UnavailableReason { get; set; }
    public bool QuoteMatched { get; set; }
}
