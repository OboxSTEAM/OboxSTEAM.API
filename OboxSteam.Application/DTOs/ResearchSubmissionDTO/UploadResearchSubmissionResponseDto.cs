namespace OboxSteam.Application.DTOs.ResearchSubmissionDTO;

/// <summary>
/// Result of uploading a research deliverable or evidence file.
/// Primary uploads return <see cref="FileUrl"/> for submit.
/// Evidence uploads return <see cref="MediaAssetId"/> (and preview URL) for submit.
/// </summary>
public class UploadResearchSubmissionResponseDto
{
    public Guid SubmissionId { get; set; }

    public string? FileUrl { get; set; }

    /// <summary>Set when <c>isEvidence=true</c> — pass into submit as EvidenceMediaAssetIds.</summary>
    public Guid? MediaAssetId { get; set; }

    /// <summary>Preview URL(s) for evidence; convenience for the client UI.</summary>
    public List<string>? EvidenceUrls { get; set; }
}
