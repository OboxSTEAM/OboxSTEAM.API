namespace OboxSteam.Application.DTOs.CurriculumReviewDTO;

public sealed class RequestCurriculumChangesRequest
{
    public Guid? SubmissionId { get; set; }

    public Guid? ConcurrencyVersion { get; set; }

    public string Comment { get; set; } = null!;

    public List<ReviewCriterionScoreRequest>? Scores { get; set; }

    public List<Guid>? RequiredChangeThreadIds { get; set; }

    public string? ClientOperationId { get; set; }
}
