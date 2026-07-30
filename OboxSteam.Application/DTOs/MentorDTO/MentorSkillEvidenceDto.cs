namespace OboxSteam.Application.DTOs.MentorDTO;

public class MentorSkillEvidenceDto
{
    public Guid Id { get; set; }
    public string Title { get; set; } = null!;
    public string? Issuer { get; set; }
    public string Url { get; set; } = null!;
    public DateTime? IssuedAt { get; set; }
    public string? CredentialId { get; set; }
}
