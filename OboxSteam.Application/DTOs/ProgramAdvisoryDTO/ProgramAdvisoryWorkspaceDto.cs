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

    public bool CanAdvise { get; set; }

    public bool CanDecide { get; set; }

    public bool CanEditCurriculum { get; set; }

    public bool CanAssignAdvisor { get; set; }

    public AdvisoryCapabilitiesDto Capabilities { get; set; } = new();

    public AdvisoryWorkflowTimelineDto Workflow { get; set; } = new();

    public int ApprovalBlockingCount { get; set; }

    public int OpenRequiredChangeCount { get; set; }

    public int AddressedRequiredChangeCount { get; set; }

    public int UnreadNoteCount { get; set; }

    public int UnreadDiscussionCount { get; set; }

    public ProgramReviewSubmissionSummaryDto? PendingSubmission { get; set; }

    public bool ReviewActionsLocked { get; set; }

    public string CollaborationContractVersion { get; set; } = "2";

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
