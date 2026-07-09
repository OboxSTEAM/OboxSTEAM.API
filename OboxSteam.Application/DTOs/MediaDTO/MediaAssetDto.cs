using OboxSteam.Domain.Enums;

namespace OboxSteam.Application.DTOs.MediaDTO;

public class MediaAssetDto
{
    public Guid Id { get; set; }
    public Guid UploaderId { get; set; }
    public Guid? ActivityId { get; set; }
    public string? FileUrl { get; set; }
    public string? FileType { get; set; }
    public VideoProcessingStatus VideoStatus { get; set; }
    public string StatusLabel { get; set; } = string.Empty;
    public bool IsReady { get; set; }
    public DateTime? UploadedAt { get; set; }
    public List<LabelTimelineEntryDto> LabelTimeline { get; set; } = new();
    public List<MediaTagDto> Tags { get; set; } = new();
}

public class LabelTimelineEntryDto
{
    public long TimestampMs { get; set; }
    public string LabelName { get; set; } = string.Empty;
    public float Confidence { get; set; }
}
