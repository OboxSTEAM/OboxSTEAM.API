using System.ComponentModel.DataAnnotations;
using OboxSteam.Domain.Enums;

namespace OboxSteam.Domain.Entities;

/// <summary>
/// Represents a payment request sent from a student to a parent.
/// The parent uses the token-based link to pay for the child's program enrollment.
/// </summary>
public class PaymentRequest : BaseEntity
{
    public Guid StudentId { get; set; }
    public User Student { get; set; } = null!;

    public Guid ParentId { get; set; }
    public User Parent { get; set; } = null!;

    public Guid? ProgramId { get; set; }
    public Program? Program { get; set; }

    public Guid? ProgramEnrollmentId { get; set; }
    public ProgramEnrollment? ProgramEnrollment { get; set; }

    public Guid? ModuleId { get; set; }
    public Module? Module { get; set; }

    public Guid? ModuleEnrollmentId { get; set; }
    public ModuleEnrollment? ModuleEnrollment { get; set; }

    public decimal Amount { get; set; }

    [MaxLength(3)]
    public string Currency { get; set; } = "VND";

    /// <summary>Secure unique token sent to parent via email link.</summary>
    [MaxLength(255)]
    public string Token { get; set; } = null!;

    public DateTime ExpiresAt { get; set; }

    public PaymentRequestStatus Status { get; set; } = PaymentRequestStatus.Pending;

    /// <summary>Linked after parent successfully completes checkout.</summary>
    public Guid? PaymentId { get; set; }
    public Payment? Payment { get; set; }
}
