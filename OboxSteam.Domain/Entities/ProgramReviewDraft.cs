using System.ComponentModel.DataAnnotations;

namespace OboxSteam.Domain.Entities;

public sealed class ProgramReviewDraft : BaseEntity
{
    public Guid SubmissionId { get; set; }
    public ProgramReviewSubmission Submission { get; set; } = null!;
    public Guid AdvisorExpertId { get; set; }
    public Expert AdvisorExpert { get; set; } = null!;
    public string ScoresJson { get; set; } = "[]";
    public string? OverallComment { get; set; }
    [ConcurrencyCheck] public Guid ConcurrencyVersion { get; set; } = Guid.NewGuid();
    public DateTime LastSavedAt { get; set; }
}
