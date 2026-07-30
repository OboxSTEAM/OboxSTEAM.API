using System.ComponentModel.DataAnnotations;

namespace OboxSteam.Domain.Entities;

/// <summary>
/// Structured evidence backing a <see cref="MentorSkill"/> (certificate, credential, or portfolio link).
/// </summary>
public class MentorSkillEvidence : BaseEntity
{
    public Guid MentorSkillId { get; set; }
    public MentorSkill MentorSkill { get; set; } = null!;

    [MaxLength(255)]
    public string Title { get; set; } = null!;

    [MaxLength(255)]
    public string? Issuer { get; set; }

    [MaxLength(2000)]
    public string Url { get; set; } = null!;

    public DateTime? IssuedAt { get; set; }

    [MaxLength(100)]
    public string? CredentialId { get; set; }
}
