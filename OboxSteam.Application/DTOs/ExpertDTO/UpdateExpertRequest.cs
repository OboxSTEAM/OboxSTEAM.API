namespace OboxSteam.Application.DTOs.ExpertDTO;

public class UpdateExpertRequest
{
    public string? Code { get; set; }
    public string? FullName { get; set; }
    public string? Title { get; set; }
    public string? Organization { get; set; }
    public string? Bio { get; set; }
    public string? AvatarUrl { get; set; }
    public string? LinkedInUrl { get; set; }
    public string? Achievements { get; set; }
    public string[]? Specialization { get; set; }
    public List<ExpertProgramAssignmentDto>? Programs { get; set; }
}
