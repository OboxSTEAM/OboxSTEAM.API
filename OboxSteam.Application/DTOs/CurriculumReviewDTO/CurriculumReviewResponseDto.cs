using OboxSteam.Domain.Enums;

namespace OboxSteam.Application.DTOs.CurriculumReviewDTO;

public sealed class CurriculumReviewResponseDto
{
    public Guid Id { get; set; }

    public Guid ProgramId { get; set; }

    public Guid ExpertId { get; set; }

    public string? ExpertName { get; set; }

    public int Round { get; set; }

    public CurriculumReviewDecision Decision { get; set; }

    public string? Comment { get; set; }

    public DateTime ReviewedAt { get; set; }

    public List<ReviewCriterionScoreResponseDto> Scores { get; set; } = [];
}
