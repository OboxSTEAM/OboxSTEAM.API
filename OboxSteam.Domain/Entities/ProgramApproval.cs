using System.ComponentModel.DataAnnotations;
using OboxSteam.Domain.Enums;

namespace OboxSteam.Domain.Entities;

/// <summary>
/// One advisor approval bound to <see cref="CurriculumVersion"/>. At most one row per program
/// has <see cref="RevokedAt"/> null.
/// </summary>
public sealed class ProgramApproval : BaseEntity
{
    public Guid ProgramId { get; set; }
    public Program Program { get; set; } = null!;

    /// <summary>Approved version (the change-list "to" version).</summary>
    public long CurriculumVersion { get; set; }

    /// <summary>Base version of the change list shown at approval time.</summary>
    public long FromVersion { get; set; }

    public Guid? FrameworkVersionId { get; set; }
    public ProgramFrameworkVersion? FrameworkVersion { get; set; }
    public string FrameworkCheckJson { get; set; } = "{}";
    public string CurriculumSnapshotJson { get; set; } = "{}";

    public Guid ApprovedByExpertId { get; set; }
    public Expert ApprovedByExpert { get; set; } = null!;
    public DateTime ApprovedAt { get; set; }
    [MaxLength(2000)] public string? Comment { get; set; }

    public DateTime? RevokedAt { get; set; }
    public Guid? RevokedByUserId { get; set; }
    public User? RevokedByUser { get; set; }
    public ProgramApprovalRevokeReason? RevokeReason { get; set; }
    [MaxLength(2000)] public string? RevokeComment { get; set; }
}
