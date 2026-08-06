using OboxSteam.Domain.Enums;

namespace OboxSteam.Application.DTOs.MediaDTO;

/// <summary>
/// Lightweight class-gallery media item for students. Omits face tags and label timelines.
/// </summary>
public class ClassGalleryMediaDto
{
    public Guid Id { get; set; }
    public Guid UploaderId { get; set; }
    public Guid ClassId { get; set; }
    public string? ClassName { get; set; }
    public Guid? ProgramId { get; set; }
    public string? ProgramName { get; set; }
    public Guid? ClassSessionId { get; set; }
    public string? FileUrl { get; set; }
    public string? FileType { get; set; }
    public VideoProcessingStatus VideoStatus { get; set; }
    public string StatusLabel { get; set; } = string.Empty;
    public bool IsReady { get; set; }
    public DateTime? UploadedAt { get; set; }
}
