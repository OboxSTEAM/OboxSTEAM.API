namespace OboxSteam.Application.DTOs.ExpertDTO;

public class ExpertCreateDto
{
    public string Code { get; set; } = null!;
    public Guid? UserId { get; set; }
    public string FullName { get; set; } = null!;
    public string? Title { get; set; }
    public string? Organization { get; set; }
    public string? Bio { get; set; }
    public string? AvatarUrl { get; set; }
    public string? LinkedInUrl { get; set; }
    public string? Achievements { get; set; }
    public List<ExpertProgramAssignmentDto> Programs { get; set; } = new();
}
