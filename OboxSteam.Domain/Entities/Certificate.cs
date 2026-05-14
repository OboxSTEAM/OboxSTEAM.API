using System.ComponentModel.DataAnnotations;

namespace OboxSteam.Domain.Entities;

/// <summary>
/// Coursera-style certificates for program or module completion.
/// </summary>
public class Certificate : BaseEntity
{
    [MaxLength(50)]
    public string Code { get; set; } = null!; // e.g., OBOX-CERT-9X8A

    public Guid StudentId { get; set; }
    public User Student { get; set; } = null!;

    /// <summary>Null if only a Module cert.</summary>
    public Guid? ProgramId { get; set; }
    public Program? Program { get; set; }

    /// <summary>Null if a full Program cert.</summary>
    public Guid? ModuleId { get; set; }
    public Module? Module { get; set; }

    public DateTime? IssueDate { get; set; }

    public string? PdfUrl { get; set; } // AWS S3 Link

    /// <summary>Public verification link (e.g., obox.id/verify/OBOX-CERT-9X8A).</summary>
    public string? VerificationUrl { get; set; }

    /// <summary>Snapshot of skills gained at time of issue.</summary>
    public string? SkillsAcquired { get; set; }
}
