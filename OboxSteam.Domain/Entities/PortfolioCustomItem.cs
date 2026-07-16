using System.ComponentModel.DataAnnotations;
using OboxSteam.Domain.Enums;

namespace OboxSteam.Domain.Entities;

public class PortfolioCustomItem : BaseEntity
{
    public Guid PortfolioId { get; set; }
    public Portfolio Portfolio { get; set; } = null!;

    public PortfolioItemType ItemType { get; set; }

    /// <summary>If InternalCertificate, maps to Certificates.Id.</summary>
    public Guid? ReferenceId { get; set; }

    /// <summary>Program this item originated from (lifetime portfolio provenance).</summary>
    public Guid? ProgramId { get; set; }
    public Program? Program { get; set; }

    public Guid? ProgramEnrollmentId { get; set; }
    public ProgramEnrollment? ProgramEnrollment { get; set; }

    public Guid? ModuleId { get; set; }
    public Module? Module { get; set; }

    public Guid? ModuleEnrollmentId { get; set; }
    public ModuleEnrollment? ModuleEnrollment { get; set; }

    /// <summary>Capstone submission when ItemType is CapstoneProject.</summary>
    public Guid? SubmissionId { get; set; }
    public Submission? Submission { get; set; }

    [MaxLength(255)]
    public string Title { get; set; } = null!;

    [MaxLength(255)]
    public string? Subtitle { get; set; }

    [MaxLength(255)]
    public string? Organization { get; set; }

    public DateTime? StartDate { get; set; }

    public DateTime? EndDate { get; set; }

    public string? Description { get; set; }

    /// <summary>Snapshot of mentor feedback at import time.</summary>
    public string? MentorEndorsement { get; set; }

    /// <summary>Student-edited narrative for abroad applications.</summary>
    public string? StudentEditedBody { get; set; }

    public string? MediaUrl { get; set; }

    public string? ExternalUrl { get; set; }

    public int DisplayOrder { get; set; }

    /// <summary>Student controls whether this item appears on the public portfolio.</summary>
    public bool IsVisible { get; set; } = true;

    public PortfolioItemSource Source { get; set; } = PortfolioItemSource.AutoImported;

    public ICollection<PortfolioItemSubmission> AppendixSubmissions { get; set; } =
        new List<PortfolioItemSubmission>();
}
