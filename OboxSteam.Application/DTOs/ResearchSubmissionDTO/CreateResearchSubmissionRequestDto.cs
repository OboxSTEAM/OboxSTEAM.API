namespace OboxSteam.Application.DTOs.ResearchSubmissionDTO;

public class CreateResearchSubmissionRequestDto
{
    public string? ContentText { get; set; }

    public string? FileUrl { get; set; }

    public List<Guid>? EvidenceMediaAssetIds { get; set; }
}
