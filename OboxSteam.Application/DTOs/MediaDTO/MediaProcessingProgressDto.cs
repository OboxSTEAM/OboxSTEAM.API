using OboxSteam.Domain.Enums;

namespace OboxSteam.Application.DTOs.MediaDTO;

/// <summary>
/// Poll response for video processing progress.
/// <see cref="PercentComplete"/> is set only during <see cref="VideoProcessingStatus.Transcoding"/>
/// (MediaConvert). Rekognition has no percent — poll <see cref="VideoStatus"/> until ready/failed.
/// </summary>
public sealed class MediaProcessingProgressDto
{
    public Guid MediaId { get; set; }
    public VideoProcessingStatus VideoStatus { get; set; }
    public string StatusLabel { get; set; } = string.Empty;

    /// <summary>
    /// MediaConvert percent 0–100 while transcoding; null for PendingTagging / non-video.
    /// </summary>
    public int? PercentComplete { get; set; }

    public bool IsReady { get; set; }
    public bool IsFailed { get; set; }
    public string? FileUrl { get; set; }
}
