using OboxSteam.Domain.Enums;

namespace OboxSteam.Application.DTOs.ProgramAdvisoryDTO;

public sealed class AdvisoryThreadEventDto
{
    public Guid Id { get; set; }
    public Guid ThreadId { get; set; }
    public long Sequence { get; set; }
    public ProgramAdvisoryThreadEventType EventType { get; set; }
    public Guid ActorUserId { get; set; }
    public ProgramAdvisoryThreadStatus? PriorStatus { get; set; }
    public ProgramAdvisoryThreadStatus? NewStatus { get; set; }
    public string? Message { get; set; }
    public AdvisoryResolutionKind? ResolutionKind { get; set; }
    public Guid? VerifiedAgainstSubmissionId { get; set; }
    public IReadOnlyList<Guid> CorrectionReferenceIds { get; set; } = [];
    public string? OperationId { get; set; }
    public DateTime CreatedAt { get; set; }
}
