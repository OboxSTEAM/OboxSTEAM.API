namespace OboxSteam.Application.DTOs.ExpertDTO;

public class ExpertCreateDto
{
    public string Code { get; set; } = null!;

    /// <summary>Optional. Link to a system user when the expert has an OBOX account; omit for external experts.</summary>
    public Guid? UserId { get; set; }
    public string FullName { get; set; } = null!;
    public string? Title { get; set; }
    public string? Organization { get; set; }
    public string? Bio { get; set; }
    public string? AvatarUrl { get; set; }
    public string? LinkedInUrl { get; set; }
    public string? Achievements { get; set; }

    /// <summary>Optional. Program board assignments; omit or send empty to create an expert without programs.</summary>
    public List<ExpertProgramAssignmentDto>? Programs { get; set; }
}
