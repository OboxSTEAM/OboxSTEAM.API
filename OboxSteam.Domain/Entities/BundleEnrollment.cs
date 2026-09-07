using OboxSteam.Domain.Enums;

namespace OboxSteam.Domain.Entities;

/// <summary>
/// Aggregate enrollment for a student's purchased pathway: roadmap, unlock
/// notifications, and pathway certificate. Child program enrollments stay on
/// <see cref="ProgramEnrollment"/> and are not FK-linked here.
/// </summary>
public class BundleEnrollment : BaseEntity
{
    public Guid StudentId { get; set; }
    public User Student { get; set; } = null!;

    public Guid BundleId { get; set; }
    public ProgramBundle Bundle { get; set; } = null!;

    public BundleEnrollmentStatus Status { get; set; } = BundleEnrollmentStatus.PendingPayment;

    /// <summary>Mean of child program enrollments, including programs owned before purchase.</summary>
    public decimal ProgressPercent { get; set; }

    public ICollection<Payment> Payments { get; set; } = new List<Payment>();
    public ICollection<PaymentRequest> PaymentRequests { get; set; } = new List<PaymentRequest>();
}
