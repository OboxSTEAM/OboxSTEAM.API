namespace OboxSteam.Application.DTOs.MediaDTO;

public class MediaTagDto
{
    public Guid Id { get; set; }
    public Guid StudentId { get; set; }
    public string? StudentName { get; set; }
    public decimal ConfidenceScore { get; set; }
    public bool IsVerified { get; set; }
    public bool HasOtherFaces { get; set; }
    public List<FaceSegmentDto> FaceSegments { get; set; } = new();
}

public class FaceSegmentDto
{
    public long StartMs { get; set; }
    public long EndMs { get; set; }
}
