namespace OboxSteam.Application.DTOs.ProgramDTO;

public class ProgramExpertSummaryDto
{
    public Guid ExpertId { get; set; }
    public string Code { get; set; } = null!;
    public string FullName { get; set; } = null!;
    public string? Title { get; set; }
    public string? Organization { get; set; }
    public string? AvatarUrl { get; set; }
    public string? LinkedInUrl { get; set; }
    public string? RoleInBoard { get; set; }
}
