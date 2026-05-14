using OboxSteam.Domain.Enums;

namespace OboxSteam.Domain.Entities;

public class CourseEnrollment : BaseEntity
{
    public Guid StudentId { get; set; }
    public User Student { get; set; } = null!;

    public Guid CourseId { get; set; }
    public Course Course { get; set; } = null!;

    public EnrollmentStatus Status { get; set; } = EnrollmentStatus.Active;

    /// <summary>Time the student booked this specific slot.</summary>
    public DateTime? JoinedAt { get; set; }

    /// <summary>Time the student checked-in (QR scan).</summary>
    public DateTime? StartedAt { get; set; }

    public DateTime? CompletedAt { get; set; }
}
