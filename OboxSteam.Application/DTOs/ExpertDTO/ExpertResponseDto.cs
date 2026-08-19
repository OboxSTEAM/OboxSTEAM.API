namespace OboxSteam.Application.DTOs.ExpertDTO;

public class ExpertResponseDto
{
    public Guid Id { get; set; }
    public string Code { get; set; } = null!;
    public Guid? UserId { get; set; }
    public string FullName { get; set; } = null!;
    public string? Title { get; set; }
    public string? Organization { get; set; }
    public string? Bio { get; set; }
    public string? AvatarUrl { get; set; }
    public string? LinkedInUrl { get; set; }
    public string? Achievements { get; set; }
    public string[] Specialization { get; set; } = [];
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public List<ExpertProgramSummaryDto> Programs { get; set; } = new();
    public List<ExpertDegreeResponseDto> Degrees { get; set; } = new();
    public List<ExpertPublicationResponseDto> Publications { get; set; } = new();
}
