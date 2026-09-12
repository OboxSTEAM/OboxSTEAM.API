using System.ComponentModel.DataAnnotations;
using OboxSteam.Domain.Enums;

namespace OboxSteam.Domain.Entities;

/// <summary>Append-only audit event for an advisory thread transition.</summary>
public sealed class ProgramAdvisoryThreadEvent : BaseEntity
{
    public Guid ProgramId { get; set; }
    public Program Program { get; set; } = null!;
    public Guid ThreadId { get; set; }
    public ProgramAdvisoryThread Thread { get; set; } = null!;
    public long Sequence { get; set; }
    public ProgramAdvisoryThreadEventType EventType { get; set; }
    public Guid ActorUserId { get; set; }
    public User ActorUser { get; set; } = null!;
    public ProgramAdvisoryThreadStatus? PriorStatus { get; set; }
    public ProgramAdvisoryThreadStatus? NewStatus { get; set; }
    [MaxLength(4000)] public string? Message { get; set; }
    public AdvisoryResolutionKind? ResolutionKind { get; set; }
    public Guid? VerifiedAgainstSubmissionId { get; set; }
    public ProgramReviewSubmission? VerifiedAgainstSubmission { get; set; }
    [MaxLength(4000)] public string? CorrectionReferenceIdsJson { get; set; }
    [MaxLength(100)] public string? OperationId { get; set; }
}
