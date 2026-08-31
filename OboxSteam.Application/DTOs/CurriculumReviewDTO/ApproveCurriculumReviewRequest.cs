namespace OboxSteam.Application.DTOs.CurriculumReviewDTO;

public sealed class ApproveCurriculumReviewRequest
{
    public string? Comment { get; set; }

    public List<ReviewCriterionScoreRequest>? Scores { get; set; }
}
