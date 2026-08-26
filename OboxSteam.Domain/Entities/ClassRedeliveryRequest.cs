using System.ComponentModel.DataAnnotations;
using OboxSteam.Domain.Enums;

namespace OboxSteam.Domain.Entities;

/// <summary>
/// Request to transfer into another cohort for hands-on / experiential re-delivery.
/// Auto-matches a later class when possible; otherwise queues for manager decision.
/// </summary>
public class ClassRedeliveryRequest : BaseEntity
{
    public Guid StudentId { get; set; }
    public User Student { get; set; } = null!;

    public Guid ModuleEnrollmentId { get; set; }
    public ModuleEnrollment ModuleEnrollment { get; set; } = null!;

    public Guid ModuleId { get; set; }
    public Module Module { get; set; } = null!;

    public Guid SourceClassId { get; set; }
    public Class SourceClass { get; set; } = null!;

    public Guid RequestedByUserId { get; set; }
    public User RequestedByUser { get; set; } = null!;

    public ClassRedeliveryRequestStatus Status { get; set; } = ClassRedeliveryRequestStatus.PendingAutoMatch;

    public Guid? TargetClassId { get; set; }
    public Class? TargetClass { get; set; }

    public Guid? PaymentId { get; set; }
    public Payment? Payment { get; set; }

    /// <summary>PendingPayment module enrollment created for retake checkout (amount = program price).</summary>
    public Guid? RetakeModuleEnrollmentId { get; set; }
    public ModuleEnrollment? RetakeModuleEnrollment { get; set; }

    /// <summary>When the student accepted intensive pace on a remedial-class offer.</summary>
    public DateTime? IntensivePaceAcceptedAt { get; set; }

    /// <summary>How the request was resolved; null until select-class or accept-intensive.</summary>
    public RedeliveryResolutionType? ResolutionType { get; set; }

    [MaxLength(1000)]
    public string? RequestMessage { get; set; }

    [MaxLength(1000)]
    public string? DecisionNote { get; set; }

    public DateTime? DecidedAt { get; set; }
    public Guid? DecidedBy { get; set; }
    public User? Decider { get; set; }
}
