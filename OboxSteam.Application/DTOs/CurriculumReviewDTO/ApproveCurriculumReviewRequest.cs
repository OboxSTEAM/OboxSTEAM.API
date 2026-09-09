namespace OboxSteam.Application.DTOs.CurriculumReviewDTO;

public sealed class ApproveCurriculumReviewRequest
{
    public Guid? SubmissionId { get; set; }

    public Guid? ConcurrencyVersion { get; set; }

    public string? Comment { get; set; }

    public List<ReviewCriterionScoreRequest>? Scores { get; set; }
}
