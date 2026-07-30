using System.ComponentModel.DataAnnotations;

namespace OboxSteam.Application.DTOs.MentorDTO;

public class MentorSkillEvidenceRequestDto
{
    [Required]
    [MaxLength(255)]
    public string Title { get; set; } = null!;

    [MaxLength(255)]
    public string? Issuer { get; set; }

    [Required]
    [MaxLength(2000)]
    public string Url { get; set; } = null!;

    public DateTime? IssuedAt { get; set; }

    [MaxLength(100)]
    public string? CredentialId { get; set; }
}
