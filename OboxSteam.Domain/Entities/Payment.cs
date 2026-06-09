using System.ComponentModel.DataAnnotations;
using OboxSteam.Domain.Enums;

namespace OboxSteam.Domain.Entities;

public class Payment : BaseEntity
{
    [MaxLength(50)]
    public string Code { get; set; } = null!; // e.g., INV-26001

    public Guid StudentId { get; set; }
    public User Student { get; set; } = null!;

    /// <summary>Null if only paying for a module retake.</summary>
    public Guid? ProgramEnrollmentId { get; set; }
    public ProgramEnrollment? ProgramEnrollment { get; set; }

    /// <summary>Non-null if this is a retake fee payment.</summary>
    public Guid? ModuleEnrollmentId { get; set; }
    public ModuleEnrollment? ModuleEnrollment { get; set; }

    public decimal Amount { get; set; }

    public PaymentGateway Gateway { get; set; }

    /// <summary>Transaction ID returned by Momo/VnPay/Stripe for reconciliation.</summary>
    [MaxLength(255)]
    public string? TransactionId { get; set; }

    public PaymentStatus Status { get; set; } = PaymentStatus.Pending;

    public DateTime? PaidAt { get; set; }

    /// <summary>Who actually paid (Student or their linked Parent).</summary>
    public Guid PaidById { get; set; }
    public User PaidBy { get; set; } = null!;

    [MaxLength(3)]
    public string Currency { get; set; } = "VND";

    /// <summary>Stripe Session ID or MoMo orderId for idempotency.</summary>
    [MaxLength(255)]
    public string? CheckoutSessionId { get; set; }

    /// <summary>Invoice generated after this payment succeeds (1:1).</summary>
    public Invoice? Invoice { get; set; }
}
