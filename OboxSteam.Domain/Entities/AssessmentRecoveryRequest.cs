using System.ComponentModel.DataAnnotations;
using OboxSteam.Domain.Enums;

namespace OboxSteam.Domain.Entities;

/// <summary>
/// Student request for extra attempts and/or a personal deadline on an assignment.
/// Grants keep the student on the same class enrollment.
/// </summary>
public class AssessmentRecoveryRequest : BaseEntity
{
    public Guid StudentId { get; set; }
    public User Student { get; set; } = null!;

    public Guid ModuleEnrollmentId { get; set; }
    public ModuleEnrollment ModuleEnrollment { get; set; } = null!;

    public Guid AssignmentId { get; set; }
    public Assignment Assignment { get; set; } = null!;

    /// <summary>Snapshot of the student's class at request time (same-class grant).</summary>
    public Guid? ClassId { get; set; }
    public Class? Class { get; set; }

    public AssessmentRecoveryRequestStatus Status { get; set; } = AssessmentRecoveryRequestStatus.Pending;

    [MaxLength(1000)]
    public string? StudentMessage { get; set; }

    [MaxLength(1000)]
    public string? MentorNote { get; set; }

    /// <summary>Extra attempts added on approve (0 allowed for deadline-only Theory grants).</summary>
    public int ExtraAttemptsGranted { get; set; }

    public DateTime? PersonalDueDate { get; set; }
    public DateTime? PersonalAvailableUntil { get; set; }

    public DateTime? DecidedAt { get; set; }
    public Guid? DecidedBy { get; set; }
    public User? Decider { get; set; }
}
