using OboxSteam.Domain.Enums;

namespace OboxSteam.Application.DTOs.ProgramAdvisoryDTO;

public sealed class ProgramAdvisoryWorkspaceDto
{
    public Guid ProgramId { get; set; }

    public ProgramStatus Status { get; set; }

    public long CurriculumVersion { get; set; }

    /// <summary>True while a live cohort (class InProgress, or Open with Active enrollments) blocks curriculum edits.</summary>
    public bool CurriculumLocked { get; set; }

    public Guid? AdvisorExpertId { get; set; }

    public string? AdvisorName { get; set; }

    /// <summary>Pinned framework version number; null without a framework.</summary>
    public int? FrameworkVersionNumber { get; set; }

    /// <summary>Highest published version of the program's framework; null without a framework.</summary>
    public int? LatestFrameworkVersionNumber { get; set; }

    public bool HasNewerFrameworkVersion { get; set; }

    public List<AdvisoryParticipantDto> Participants { get; set; } = [];

    public AdvisoryCapabilitiesDto Capabilities { get; set; } = new();

    /// <summary>The active (non-revoked) approval, or null.</summary>
    public ProgramApprovalSummaryDto? Approval { get; set; }

    public int OpenPinCount { get; set; }

    public int AddressedPinCount { get; set; }

    /// <summary>Discussion messages after the caller's read cursor, excluding their own.</summary>
    public int UnreadCount { get; set; }

    /// <summary>Live framework check; false when the pinned framework version is unavailable.</summary>
    public bool FrameworkCheckPassed { get; set; }

    /// <summary>Net change items since the latest approval (<c>base=lastApproval</c>).</summary>
    public int ChangesSinceApprovalCount { get; set; }

    /// <summary>Net change items since the latest approval that the caller has not seen.</summary>
    public int UnseenChangeCount { get; set; }

    public long LatestSequence { get; set; }
}

public sealed class AdvisoryParticipantDto
{
    public Guid UserId { get; set; }

    public string Name { get; set; } = null!;

    public RoleType Role { get; set; }

    public bool IsAdvisor { get; set; }
}

public sealed class ProgramApprovalSummaryDto
{
    public Guid Id { get; set; }

    public long CurriculumVersion { get; set; }

    public DateTime ApprovedAt { get; set; }

    public string? ApprovedByName { get; set; }

    public string? Comment { get; set; }
}
