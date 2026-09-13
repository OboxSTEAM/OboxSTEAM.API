namespace OboxSteam.Application.DTOs.ProgramBundleDTO;

/// <summary>Pathway certificate when the bundle enrollment is completed and issued.</summary>
public sealed class MyBundlePathwayCertificateDto
{
    public Guid Id { get; set; }

    public string Code { get; set; } = null!;

    public DateTime? IssueDate { get; set; }

    public string? PdfUrl { get; set; }

    public string? VerificationUrl { get; set; }
}
