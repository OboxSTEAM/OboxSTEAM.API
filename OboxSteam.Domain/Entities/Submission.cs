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

    /// <summary>The module enrollment attempt this submission was made under (optional).</summary>
    public Guid? ModuleEnrollmentId { get; set; }
    public ModuleEnrollment? ModuleEnrollment { get; set; }

    /// <summary>Distinguishes resubmissions; incremented each time the student submits again.</summary>
    public int AttemptNumber { get; set; } = 1;

    public SubmissionStatus Status { get; set; } = SubmissionStatus.Pending;

    public string? ContentText { get; set; }

    public string? FileUrl { get; set; }

    public decimal? AssignedGrade { get; set; }

    public string? MentorFeedback { get; set; }

    /// <summary>Mentor ID who graded/verified this submission.</summary>
    public Guid? VerifiedBy { get; set; }
    public User? Verifier { get; set; }

    /// <summary>When the student started this attempt (timer begins).</summary>
    public DateTime? StartedAt { get; set; }

    /// <summary>
    /// When this attempt expires (<c>StartedAt</c> plus <c>Assignment.TimeLimitMinutes</c>).
    /// Null on legacy rows; those drafts do not hold AcademicFail.
    /// </summary>
    public DateTime? ExpiresAt { get; set; }

    public DateTime? SubmittedAt { get; set; }

    /// <summary>Research milestone this submission belongs to (null for quiz/non-research work).</summary>
    public Guid? ResearchMilestoneId { get; set; }
    public ResearchMilestone? ResearchMilestone { get; set; }

    /// <summary>When the mentor graded this submission.</summary>
    public DateTime? GradedAt { get; set; }

    // Navigation
    public ICollection<SubmissionEvidence> SubmissionEvidences { get; set; } = new List<SubmissionEvidence>();
    public ICollection<QuizQuestion> QuizQuestions { get; set; } = new List<QuizQuestion>();
    public ICollection<QuizAnswer> QuizAnswers { get; set; } = new List<QuizAnswer>();
}
