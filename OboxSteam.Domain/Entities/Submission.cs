using System.ComponentModel.DataAnnotations;
using OboxSteam.Domain.Enums;

namespace OboxSteam.Domain.Entities;

public class Submission : BaseEntity
{
    [MaxLength(50)]
    public string Code { get; set; } = null!;

    public Guid AssignmentId { get; set; }
    public Assignment Assignment { get; set; } = null!;

    public Guid StudentId { get; set; }
    public User Student { get; set; } = null!;

    public SubmissionStatus Status { get; set; } = SubmissionStatus.Pending;

    public string? ContentText { get; set; }

    public string? FileUrl { get; set; }

    public decimal? AssignedGrade { get; set; }

    public string? MentorFeedback { get; set; }

    /// <summary>Mentor ID who graded/verified this submission.</summary>
    public Guid? VerifiedBy { get; set; }
    public User? Verifier { get; set; }

    public DateTime? SubmittedAt { get; set; }

    // Navigation
    public ICollection<SubmissionEvidence> SubmissionEvidences { get; set; } = new List<SubmissionEvidence>();
}
