using OboxSteam.Domain.Enums;

namespace OboxSteam.Application.DTOs.ClassRedeliveryDTO;

public class ClassRedeliveryRequestResponseDto
{
    public Guid Id { get; set; }
    public Guid StudentId { get; set; }
    public Guid ModuleEnrollmentId { get; set; }
    public Guid ModuleId { get; set; }
    public Guid SourceClassId { get; set; }
    public Guid RequestedByUserId { get; set; }
    public ClassRedeliveryRequestStatus Status { get; set; }
    public Guid? TargetClassId { get; set; }
    public Guid? PaymentId { get; set; }
    public Guid? RetakeModuleEnrollmentId { get; set; }
    public DateTime? IntensivePaceAcceptedAt { get; set; }
    public RedeliveryResolutionType? ResolutionType { get; set; }
    public string? RequestMessage { get; set; }
    public string? DecisionNote { get; set; }
    public DateTime? DecidedAt { get; set; }
    public Guid? DecidedBy { get; set; }
    public DateTime CreatedAt { get; set; }
}
