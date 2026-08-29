using OboxSteam.Domain.Enums;

namespace OboxSteam.Domain.Entities;

public class ProgramEnrollment : BaseEntity
{
    public Guid StudentId { get; set; }
    public User Student { get; set; } = null!;

    public Guid ProgramId { get; set; }
    public Program Program { get; set; } = null!;

    public EnrollmentStatus Status { get; set; } = EnrollmentStatus.Active;

    public decimal ProgressPercent { get; set; }

    /// <summary>Time the student bought/registered.</summary>
    public DateTime? EnrolledAt { get; set; }

    /// <summary>Time the student viewed first material.</summary>
    public DateTime? StartedAt { get; set; }

    /// <summary>Time the student finished all requirements.</summary>
    public DateTime? CompletedAt { get; set; }

    /// <summary>Why this purchase was closed. Null while the enrollment is still open.</summary>
    public ProgramPurchaseEndReason? EndReason { get; set; }

    /// <summary>Module whose failure closed the purchase. Null for Withdraw.</summary>
    public Guid? EndedModuleId { get; set; }
    public Module? EndedModule { get; set; }

    /// <summary>Time the purchase was closed (Failed/Dropped).</summary>
    public DateTime? EndedAt { get; set; }

    /// <summary>Previous closed purchase this rebuy carries progress from.</summary>
    public Guid? SourceProgramEnrollmentId { get; set; }
    public ProgramEnrollment? SourceProgramEnrollment { get; set; }

    // Navigation
    public ICollection<Payment> Payments { get; set; } = new List<Payment>();
    public ICollection<ModuleEnrollment> ModuleEnrollments { get; set; } = new List<ModuleEnrollment>();
    public ICollection<ClassEnrollment> ClassEnrollments { get; set; } = new List<ClassEnrollment>();
}
