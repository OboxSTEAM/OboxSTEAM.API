using OboxSteam.Domain.Enums;

namespace OboxSteam.Domain.Entities;

public class ModuleEnrollment : BaseEntity
{
    public Guid StudentId { get; set; }
    public User Student { get; set; } = null!;

    public Guid ModuleId { get; set; }
    public Module Module { get; set; } = null!;

    /// <summary>The program enrollment this module attempt belongs to.</summary>
    public Guid ProgramEnrollmentId { get; set; }
    public ProgramEnrollment ProgramEnrollment { get; set; } = null!;

    public EnrollmentStatus Status { get; set; } = EnrollmentStatus.Active;

    public decimal ProgressPercent { get; set; }

    /// <summary>Final grade for this enrollment attempt.</summary>
    public decimal? FinalGrade { get; set; }

    /// <summary>Which attempt this enrollment represents; incremented on retake after fail.</summary>
    public int AttemptNumber { get; set; } = 1;

    /// <summary>Number of failed assignment attempts within the current module attempt.</summary>
    public int AssignmentFailureCount { get; set; }

    public DateTime? EnrolledAt { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }

    // Navigation
    public ICollection<Payment> Payments { get; set; } = new List<Payment>();
    public ICollection<ActivityProgress> ActivityProgresses { get; set; } = new List<ActivityProgress>();
    public ICollection<Submission> Submissions { get; set; } = new List<Submission>();
}
