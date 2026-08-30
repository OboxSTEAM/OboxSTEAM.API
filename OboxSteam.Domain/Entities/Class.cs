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

    /// <summary>
    /// Assigned mentor. Null while the class is open for mentor requests;
    /// set when a request is approved or when a manager assigns directly.
    /// </summary>
    public Guid? MentorId { get; set; }
    public User? Mentor { get; set; }

    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }

    public int MaxCapacity { get; set; }

    public ClassKind Kind { get; set; } = ClassKind.Standard;

    /// <summary>
    /// When <see cref="Kind"/> is Remedial, the single module this class re-delivers.
    /// Null for Standard classes.
    /// </summary>
    public Guid? RemedialModuleId { get; set; }
    public Module? RemedialModule { get; set; }

    public ClassStatus Status { get; set; } = ClassStatus.Draft;

    /// <summary>
    /// Generate buffer (hours) before the first teaching session. Late-join
    /// uses two-thirds of each open AssignmentWindow, not this field.
    /// </summary>
    public int MinHoursBeforeAssignmentJoin { get; set; } = 48;

    /// <summary>Human-readable schedule for class picker UI, e.g. "Every Saturday 9:00–12:00".</summary>
    [MaxLength(255)]
    public string? ScheduleSummary { get; set; }

    // Navigation
    public ICollection<ClassEnrollment> ClassEnrollments { get; set; } = new List<ClassEnrollment>();
    public ICollection<ClassSession> ClassSessions { get; set; } = new List<ClassSession>();
    public ICollection<ClassSkill> ClassSkills { get; set; } = new List<ClassSkill>();
    public ICollection<ClassMentorRequest> MentorRequests { get; set; } = new List<ClassMentorRequest>();
}
