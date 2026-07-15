namespace OboxSteam.Application.DTOs.CertificateDTO;

/// <summary>Input snapshot for PDF rendering (Infrastructure generator).</summary>
public sealed class CertificatePdfModel
{
    public string Code { get; set; } = null!;

    public string StudentFullName { get; set; } = null!;

    public string? StudentAvatarUrl { get; set; }

    public string IssuerLogoUrl { get; set; } = CertificateBranding.IssuerLogoUrl;

    public string ProgramName { get; set; } = null!;

    public string? ProgramDescription { get; set; }

    public string? ProgramThumbnailUrl { get; set; }

    public DateTime IssueDate { get; set; }

    public string VerificationUrl { get; set; } = null!;

    public List<string> ModuleNames { get; set; } = [];
}
