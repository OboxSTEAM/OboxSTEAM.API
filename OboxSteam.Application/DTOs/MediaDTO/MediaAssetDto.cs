using OboxSteam.Domain.Enums;

namespace OboxSteam.Application.DTOs.MediaDTO;

public class MediaAssetDto
{
    public Guid Id { get; set; }
    public Guid UploaderId { get; set; }
    public Guid? ActivityId { get; set; }
    public string? FileUrl { get; set; }
    public string? FileType { get; set; }
    public string? FaceSearchJobId { get; set; }
    public VideoProcessingStatus VideoStatus { get; set; }
    public DateTime? UploadedAt { get; set; }
    public List<MediaTagDto> Tags { get; set; } = new();
}
