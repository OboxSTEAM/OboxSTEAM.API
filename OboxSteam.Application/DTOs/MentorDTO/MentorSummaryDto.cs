namespace OboxSteam.Application.DTOs.MentorDTO;

/// <summary>
/// Parent-safe mentor summary embedded in class responses (no email/phone/usage stats).
/// </summary>
public class MentorSummaryDto
{
    public Guid Id { get; set; }
    public string? FullName { get; set; }
    public string? AvatarUrl { get; set; }
    public string? Title { get; set; }
    public string? Organization { get; set; }
    public string? Bio { get; set; }
    public string? Achievements { get; set; }
    public string? LinkedInUrl { get; set; }
}
