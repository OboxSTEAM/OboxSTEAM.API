namespace OboxSteam.Application.DTOs.MediaDTO;

/// <summary>
/// Eligible class video for the add-segment picker on a highlight stack.
/// </summary>
public class HighlightSourceMediaDto
{
    public Guid MediaId { get; set; }
    public string? FileUrl { get; set; }
    public Guid ClassId { get; set; }
    public Guid? ClassSessionId { get; set; }
    public long? DurationMs { get; set; }
    public DateTime? UploadedAt { get; set; }
    public IReadOnlyList<HighlightFaceSegmentDto> FaceSegments { get; set; } = Array.Empty<HighlightFaceSegmentDto>();
}

public class HighlightFaceSegmentDto
{
    public long StartMs { get; set; }
    public long EndMs { get; set; }
}