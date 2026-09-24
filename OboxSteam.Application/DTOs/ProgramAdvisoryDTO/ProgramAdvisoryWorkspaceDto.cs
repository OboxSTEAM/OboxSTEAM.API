using OboxSteam.Domain.Enums;

namespace OboxSteam.Application.DTOs.ProgramAdvisoryDTO;

public sealed class ProgramAdvisoryWorkspaceDto
{
    public Guid ProgramId { get; set; }

    public string Code { get; set; } = null!;

    public string Name { get; set; } = null!;

    public ProgramStatus Status { get; set; }

    public Guid? AdvisorExpertId { get; set; }

    public string? AdvisorName { get; set; }

    public Guid? FrameworkVersionId { get; set; }

    public int? FrameworkVersionNumber { get; set; }

    public List<AdvisoryParticipantDto> Participants { get; set; } = [];

    public AdvisoryCapabilitiesDto Capabilities { get; set; } = new();

    public AdvisoryWorkflowTimelineDto Workflow { get; set; } = new();

    /// <summary>
    /// RequiredChange threads that are not yet accepted
    /// (<see cref="ProgramAdvisoryThreadStatus.Open"/> + <see cref="ProgramAdvisoryThreadStatus.Addressed"/>).
    /// Matches <c>scope=outstanding</c> and timeline <c>outstandingRequirementCount</c>.
    /// </summary>
    public int OutstandingRequiredCount { get; set; }

    /// <summary>RequiredChange threads the manager has marked fixed (Addressed) and the advisor has not accepted.</summary>
    public int FixedRequiredCount { get; set; }

    public int OpenRequiredChangeCount { get; set; }

    public int AddressedRequiredChangeCount { get; set; }

    /// <summary>Unread note threads, including the general discussion thread.</summary>
    public int UnreadNoteCount { get; set; }

    public ProgramReviewSubmissionSummaryDto? PendingSubmission { get; set; }

    public bool ReviewActionsLocked { get; set; }

    public string CollaborationContractVersion { get; set; } = "3";

    public ProgramReviewSubmissionSummaryDto? LatestSubmission { get; set; }

    public AdvisoryFeedbackCountsDto FeedbackCounts { get; set; } = new();

    public bool HasUnreadFeedback { get; set; }
}

public sealed class AdvisoryParticipantDto
{
    public Guid UserId { get; set; }

    public Guid? ExpertId { get; set; }

    public string DisplayName { get; set; } = null!;

    public string Role { get; set; } = null!;

    public bool IsAdvisor { get; set; }
}

public sealed class AdvisoryFeedbackCountsDto
{
    public int OpenSuggestions { get; set; }

    public int AddressedSuggestions { get; set; }

    public int ResolvedSuggestions { get; set; }

    public int OpenRequiredChanges { get; set; }

    public int AddressedRequiredChanges { get; set; }

    public int ResolvedRequiredChanges { get; set; }
}
