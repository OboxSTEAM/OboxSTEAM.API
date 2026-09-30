namespace OboxSteam.Application.DTOs.ProgramAdvisoryDTO;

public sealed class ProgramReviewDraftDto
{
    public Guid? Id { get; set; }

    public Guid SubmissionId { get; set; }

    public string? OverallComment { get; set; }

    public Guid ConcurrencyVersion { get; set; }

    public DateTime? LastSavedAt { get; set; }
}

public sealed class SaveProgramReviewDraftRequest
{
    public string? OverallComment { get; set; }

    public Guid ConcurrencyVersion { get; set; }
}
