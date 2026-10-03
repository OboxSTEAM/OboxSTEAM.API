using OboxSteam.Domain.Enums;

namespace OboxSteam.Application.DTOs.ResearchSubmissionDTO;

/// <summary>One linked evidence media item, with its id and preview URL kept together.</summary>
public class ResearchSubmissionEvidenceDto
{
    public Guid MediaAssetId { get; set; }

    /// <summary>Null while a video is still transcoding or when transcoding failed.</summary>
    public string? FileUrl { get; set; }

    public string? FileType { get; set; }

    public VideoProcessingStatus VideoStatus { get; set; }
}
