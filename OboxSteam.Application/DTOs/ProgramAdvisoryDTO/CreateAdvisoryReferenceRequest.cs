using OboxSteam.Domain.Enums;

namespace OboxSteam.Application.DTOs.ProgramAdvisoryDTO;

public sealed class CreateAdvisoryReferenceRequest
{
    public AdvisoryReferenceContext Context { get; set; }
    public Guid? SubmissionId { get; set; }
    public ProgramAdvisoryTargetType TargetType { get; set; }
    public Guid? TargetId { get; set; }
    public ProgramAdvisoryAnchorKind AnchorKind { get; set; }
    public string? FieldKey { get; set; }
    public string? Quote { get; set; }
    public string? QuotePrefix { get; set; }
    public string? QuoteSuffix { get; set; }
}
