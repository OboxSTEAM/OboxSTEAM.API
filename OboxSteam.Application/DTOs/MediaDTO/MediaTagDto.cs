namespace OboxSteam.Application.DTOs.MediaDTO;

public class MediaTagDto
{
    public Guid StudentId { get; set; }
    public string? StudentName { get; set; }
    public decimal ConfidenceScore { get; set; }
    public bool IsVerified { get; set; }
}
