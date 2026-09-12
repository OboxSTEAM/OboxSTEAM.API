using OboxSteam.Domain.Enums;

namespace OboxSteam.Application.DTOs.ProgramAdvisoryDTO;

public sealed class AdvisoryThreadDto
{
    public Guid Id { get; set; }

    public Guid ProgramId { get; set; }

    public Guid? SubmissionId { get; set; }

    public Guid AuthorUserId { get; set; }

    public string? AuthorName { get; set; }

    public ProgramAdvisoryTargetType TargetType { get; set; }

    public Guid? TargetId { get; set; }

    public string TargetLabel { get; set; } = null!;

    public string? TargetContext { get; set; }

    public ProgramAdvisoryThreadType Type { get; set; }

    public ProgramAdvisoryThreadStatus Status { get; set; }

    public ProgramAdvisoryAnchorKind? AnchorKind { get; set; }

    public string? AnchorField { get; set; }

    public string? AnchorQuote { get; set; }

    public string? LatestMessagePreview { get; set; }

    public DateTime LastMessageAt { get; set; }

    public DateTime CreatedAt { get; set; }

    public int MessageCount { get; set; }

    public Guid ConcurrencyVersion { get; set; }

    public long LatestActivitySequence { get; set; }

    /// <summary>Submission number of the round that originally opened this thread (carried requirements).</summary>
    public int? OriginSubmissionNumber { get; set; }

    /// <summary>Review-round intent of the origin submission when known.</summary>
    public ProgramReviewSubmissionIntent? OriginReviewRoundIntent { get; set; }

    /// <summary>Short label such as <c>Round 1 · InitialReview</c> for carried outstanding requirements.</summary>
    public string? OriginRoundLabel { get; set; }

    public bool CanAddress { get; set; }

    public bool CanResolve { get; set; }

    public bool CanReopen { get; set; }

    public bool CanWaive { get; set; }

    /// <summary>Ordered lifecycle events. Present on detail and list responses.</summary>
    public List<AdvisoryThreadEventDto> Events { get; set; } = [];

    /// <summary>
    /// Ordered note messages. Populated on thread detail GET so clients do not need to
    /// merge <c>MessageAdded</c> events with a separate message list. Empty on list endpoints.
    /// </summary>
    public List<AdvisoryMessageDto> Messages { get; set; } = [];
}
