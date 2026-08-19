namespace OboxSteam.Application.DTOs.ExpertDTO;

public class ExpertDegreeResponseDto
{
    public Guid Id { get; set; }
    public Guid ExpertId { get; set; }
    public string Title { get; set; } = null!;
    public string Institution { get; set; } = null!;
    public int Year { get; set; }
}
