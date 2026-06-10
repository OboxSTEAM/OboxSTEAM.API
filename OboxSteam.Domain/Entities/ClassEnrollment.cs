using OboxSteam.Domain.Enums;

namespace OboxSteam.Domain.Entities;

/// <summary>
/// Links a student to a cohort. One active class enrollment per program enrollment in v1.
/// </summary>
public class ClassEnrollment : BaseEntity
{
    public Guid ClassId { get; set; }
    public Class Class { get; set; } = null!;

    public Guid StudentId { get; set; }
    public User Student { get; set; } = null!;

    public Guid ProgramEnrollmentId { get; set; }
    public ProgramEnrollment ProgramEnrollment { get; set; } = null!;

    public ClassEnrollmentStatus Status { get; set; } = ClassEnrollmentStatus.Active;

    public DateTime? EnrolledAt { get; set; }
}
