namespace OboxSteam.Application.DTOs.ExpertDTO;

public class ExpertPublicationResponseDto
{
    public Guid Id { get; set; }
    public Guid ExpertId { get; set; }
    public string Title { get; set; } = null!;
    public string? Venue { get; set; }
    public int Year { get; set; }
    public string? Url { get; set; }
}
