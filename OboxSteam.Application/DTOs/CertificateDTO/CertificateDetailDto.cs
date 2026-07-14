namespace OboxSteam.Application.DTOs.CertificateDTO;

/// <summary>
/// Full payload for the certificate show / verify page (FE renders share and download UX).
/// </summary>
public sealed class CertificateDetailDto
{
    public Guid Id { get; set; }

    public string Code { get; set; } = null!;

    public DateTime? IssueDate { get; set; }

    public string? PdfUrl { get; set; }

    public string? VerificationUrl { get; set; }

    public string? SkillsAcquired { get; set; }

    public string IssuerName { get; set; } = "OboxSTEAM";

    public CertificateStudentDto Student { get; set; } = null!;

    public CertificateProgramDto Program { get; set; } = null!;

    public List<CertificateModuleDto> Modules { get; set; } = [];

    public List<string> LearningOutcomes { get; set; } = [];

    public List<string> SkillsGained { get; set; } = [];
}

public sealed class CertificateStudentDto
{
    public Guid Id { get; set; }

    public string? FullName { get; set; }

    public string? AvatarUrl { get; set; }
}

public sealed class CertificateProgramDto
{
    public Guid Id { get; set; }

    public string Name { get; set; } = null!;

    public string? Description { get; set; }

    public string? EstimatedDuration { get; set; }

    public string? ThumbnailUrl { get; set; }
}

public sealed class CertificateModuleDto
{
    public Guid ModuleId { get; set; }

    public string Name { get; set; } = null!;

    public int ModuleOrder { get; set; }
}

/// <summary>Compact row for certificate list endpoints.</summary>
public sealed class CertificateListItemDto
{
    public Guid Id { get; set; }

    public string Code { get; set; } = null!;

    public Guid ProgramId { get; set; }

    public string ProgramName { get; set; } = null!;

    public DateTime? IssueDate { get; set; }

    public string? PdfUrl { get; set; }

    public string? VerificationUrl { get; set; }
}
