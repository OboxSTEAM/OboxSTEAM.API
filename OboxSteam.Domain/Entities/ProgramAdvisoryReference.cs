using System.ComponentModel.DataAnnotations;
using OboxSteam.Domain.Enums;

namespace OboxSteam.Domain.Entities;

/// <summary>Immutable context captured for a note or Discussion reference.</summary>
public sealed class ProgramAdvisoryReference : BaseEntity
{
    public Guid ProgramId { get; set; }
    public Program Program { get; set; } = null!;
    public AdvisoryReferenceContext Context { get; set; }
    public Guid? SubmissionId { get; set; }
    public ProgramReviewSubmission? Submission { get; set; }
    public ProgramAdvisoryTargetType TargetType { get; set; }
    public Guid TargetId { get; set; }
    public ProgramAdvisoryAnchorKind AnchorKind { get; set; }
    [MaxLength(100)] public string? FieldKey { get; set; }
    [MaxLength(2000)] public string? Quote { get; set; }
    [MaxLength(1000)] public string? QuotePrefix { get; set; }
    [MaxLength(1000)] public string? QuoteSuffix { get; set; }
    [MaxLength(255)] public string CapturedLabel { get; set; } = null!;
    [MaxLength(4000)] public string? CapturedExcerpt { get; set; }
    public DateTime CapturedAt { get; set; }
}
