namespace OboxSteam.Application.DTOs.ResearchSubmissionDTO;

/// <summary>
/// Result of uploading a research deliverable or evidence file.
/// <see cref="FileUrl"/> / <see cref="EvidenceUrls"/> are passed into submit.
/// </summary>
public class UploadResearchSubmissionResponseDto
{
    public Guid SubmissionId { get; set; }

    public string? FileUrl { get; set; }

    public List<string>? EvidenceUrls { get; set; }
}
