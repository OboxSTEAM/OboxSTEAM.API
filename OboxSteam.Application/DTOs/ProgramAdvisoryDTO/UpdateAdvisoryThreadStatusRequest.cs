using OboxSteam.Domain.Enums;

namespace OboxSteam.Application.DTOs.ProgramAdvisoryDTO;

public sealed class UpdateAdvisoryThreadStatusRequest
{
    public ProgramAdvisoryThreadStatus Status { get; set; }

    public string? Message { get; set; }

    public Guid? ConcurrencyVersion { get; set; }

    public AdvisoryResolutionKind? ResolutionKind { get; set; }

    public Guid? VerifiedAgainstSubmissionId { get; set; }

    public List<Guid>? CorrectionReferenceIds { get; set; }

    public string? ClientOperationId { get; set; }
}
