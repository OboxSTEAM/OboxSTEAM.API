namespace OboxSteam.Application.DTOs.CurriculumReviewDTO;

public sealed class ReviewCriterionScoreRequest
{
    public Guid CriterionId { get; set; }

    public int Score { get; set; }

    public string? Comment { get; set; }
}
