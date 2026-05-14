using System.ComponentModel.DataAnnotations;

namespace OboxSteam.Domain.Entities;

public class Course : BaseEntity
{
    [MaxLength(50)]
    public string Code { get; set; } = null!;

    public Guid ModuleId { get; set; }
    public Module Module { get; set; } = null!;

    public Guid MentorId { get; set; }
    public User Mentor { get; set; } = null!;

    [MaxLength(255)]
    public string Name { get; set; } = null!;

    public string? Description { get; set; }

    // Navigation
    public ICollection<Activity> Activities { get; set; } = new List<Activity>();
    public ICollection<Assignment> Assignments { get; set; } = new List<Assignment>();
    public ICollection<CourseEnrollment> CourseEnrollments { get; set; } = new List<CourseEnrollment>();
}
