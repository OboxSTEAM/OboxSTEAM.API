namespace OboxSteam.Application.DTOs.CurriculumReviewDTO;

public sealed class ReviewCriterionScoreResponseDto
{
    public Guid Id { get; set; }

    public Guid CriterionId { get; set; }

    public string? CriterionName { get; set; }

    public int Score { get; set; }

    public int MaxScore { get; set; }

    public string? Comment { get; set; }
}
