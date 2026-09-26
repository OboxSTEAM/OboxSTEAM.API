using OboxSteam.Domain.Enums;

namespace OboxSteam.Application.DTOs.PortfolioDTO;

public class SkillEvidenceDto
{
    public SkillEvidenceType Type { get; set; }

    /// <summary>Omitted on the public portfolio. Owners still receive it.</summary>
    public Guid? ProgramId { get; set; }

    public string? ProgramName { get; set; }

    public string? CertificateCode { get; set; }

    public string? VerificationUrl { get; set; }

    public Guid? PortfolioItemId { get; set; }

    public DateTime AchievedAt { get; set; }
}
