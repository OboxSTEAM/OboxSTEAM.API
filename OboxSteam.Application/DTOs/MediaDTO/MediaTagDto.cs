namespace OboxSteam.Application.DTOs.MediaDTO;

public class MediaTagDto
{
    public Guid Id { get; set; }
    public Guid StudentId { get; set; }
    public string? StudentName { get; set; }
    public decimal ConfidenceScore { get; set; }
    public bool IsVerified { get; set; }
    public string? FaceSegmentsJson { get; set; }
    public bool HasOtherFaces { get; set; }
    public string? MappedSpeakerLabel { get; set; }
    public string? VoiceSegmentsJson { get; set; }
}
