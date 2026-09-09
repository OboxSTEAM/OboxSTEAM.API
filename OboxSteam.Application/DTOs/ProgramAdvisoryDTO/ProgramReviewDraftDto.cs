using OboxSteam.Application.DTOs.CurriculumReviewDTO;

namespace OboxSteam.Application.DTOs.ProgramAdvisoryDTO;

public sealed class ProgramReviewDraftDto
{
    public Guid? Id { get; set; }

    public Guid SubmissionId { get; set; }

    public List<ReviewCriterionScoreRequest> Scores { get; set; } = [];

    public string? OverallComment { get; set; }

    public Guid ConcurrencyVersion { get; set; }

    public DateTime? LastSavedAt { get; set; }
}

public sealed class SaveProgramReviewDraftRequest
{
    public List<ReviewCriterionScoreRequest>? Scores { get; set; }

    public string? OverallComment { get; set; }

    public Guid ConcurrencyVersion { get; set; }
}
