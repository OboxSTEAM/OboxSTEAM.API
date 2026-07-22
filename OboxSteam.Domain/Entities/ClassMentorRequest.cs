using System.ComponentModel.DataAnnotations;
using OboxSteam.Domain.Enums;

namespace OboxSteam.Domain.Entities;

/// <summary>
/// Mentor application to be assigned to an unassigned class cohort.
/// Manager approves exactly one request per class (sets <see cref="Class.MentorId"/>).
/// </summary>
public class ClassMentorRequest : BaseEntity
{
    public Guid ClassId { get; set; }
    public Class Class { get; set; } = null!;

    public Guid MentorId { get; set; }
    public User Mentor { get; set; } = null!;

    public ClassMentorRequestStatus Status { get; set; } = ClassMentorRequestStatus.Pending;

    /// <summary>Optional note from the mentor explaining fit / availability.</summary>
    [MaxLength(1000)]
    public string? Message { get; set; }

    public DateTime? DecidedAt { get; set; }

    public Guid? DecidedBy { get; set; }
    public User? Decider { get; set; }

    /// <summary>Optional note from the manager when approving or rejecting.</summary>
    [MaxLength(1000)]
    public string? DecisionNote { get; set; }
}
