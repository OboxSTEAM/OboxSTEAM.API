using System.ComponentModel.DataAnnotations;
using OboxSteam.Domain.Enums;

namespace OboxSteam.Domain.Entities;

/// <summary>
/// A running cohort / classroom instance of a program (đợt học).
/// Defines when a group of students moves through the curriculum together.
/// </summary>
public class Class : BaseEntity
{
    [MaxLength(50)]
    public string Code { get; set; } = null!;

    [MaxLength(255)]
    public string Name { get; set; } = null!;

    public Guid ProgramId { get; set; }
    public Program Program { get; set; } = null!;

    public Guid MentorId { get; set; }
    public User Mentor { get; set; } = null!;

    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }

    /// <summary>IANA timezone id, e.g. Asia/Ho_Chi_Minh.</summary>
    [MaxLength(64)]
    public string Timezone { get; set; } = "Asia/Ho_Chi_Minh";

    public int MaxCapacity { get; set; }

    public ClassStatus Status { get; set; } = ClassStatus.Draft;

    /// <summary>
    /// Minimum hours before the next AssignmentWindow session during which
    /// a student may not self-enroll (late-join guard). Manager may bypass.
    /// </summary>
    public int MinHoursBeforeAssignmentJoin { get; set; } = 48;

    /// <summary>Human-readable schedule for class picker UI, e.g. "Every Saturday 9:00–12:00".</summary>
    [MaxLength(255)]
    public string? ScheduleSummary { get; set; }

    [MaxLength(255)]
    public string? LocationSummary { get; set; }

    // Navigation
    public ICollection<ClassEnrollment> ClassEnrollments { get; set; } = new List<ClassEnrollment>();
    public ICollection<ClassSession> ClassSessions { get; set; } = new List<ClassSession>();
}
