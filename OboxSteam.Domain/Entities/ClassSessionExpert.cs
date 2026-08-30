using OboxSteam.Domain.Enums;

namespace OboxSteam.Domain.Entities;

/// <summary>
/// Expert co-teach invitation on a class session, including private mentor feedback
/// after the session is completed. Students must not see feedback fields.
/// </summary>
public class ClassSessionExpert : BaseEntity
{
    public Guid ClassSessionId { get; set; }
    public ClassSession ClassSession { get; set; } = null!;

    public Guid ExpertId { get; set; }
    public Expert Expert { get; set; } = null!;

    public ClassSessionExpertStatus Status { get; set; } = ClassSessionExpertStatus.Invited;

    public string? MentorFeedback { get; set; }

    /// <summary>1–5 when feedback is present; otherwise null.</summary>
    public int? MentorFeedbackRating { get; set; }

    public DateTime? MentorFeedbackAt { get; set; }
}
