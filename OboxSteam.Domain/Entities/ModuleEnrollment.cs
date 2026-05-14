using OboxSteam.Domain.Enums;

namespace OboxSteam.Domain.Entities;

public class ModuleEnrollment : BaseEntity
{
    public Guid StudentId { get; set; }
    public User Student { get; set; } = null!;

    public Guid ModuleId { get; set; }
    public Module Module { get; set; } = null!;

    public EnrollmentStatus Status { get; set; } = EnrollmentStatus.Active;

    public decimal ProgressPercent { get; set; }

    /// <summary>Final grade for this enrollment attempt.</summary>
    public decimal? FinalGrade { get; set; }

    public DateTime? EnrolledAt { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }

    // Navigation
    public ICollection<Payment> Payments { get; set; } = new List<Payment>();
}
