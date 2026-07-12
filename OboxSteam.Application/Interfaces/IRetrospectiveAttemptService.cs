using OboxSteam.Application.DTOs.RetrospectiveDTO;

namespace OboxSteam.Application.Interfaces;

/// <summary>
/// Student retrospective attempt flow with plain-text draft saving.
/// </summary>
public interface IRetrospectiveAttemptService
{
    /// <summary>
    /// Starts a new draft or resumes an existing <c>Pending</c> or <c>ReturnedForRevision</c> submission.
    /// </summary>
    Task<RetrospectiveAttemptResponseDto> StartRetrospective(Guid assignmentId);

    /// <summary>
    /// Returns the retrospective submission for the current user (student or authorized staff/parent).
    /// </summary>
    Task<RetrospectiveAttemptResponseDto?> GetRetrospective(Guid submissionId);

    /// <summary>
    /// Saves plain-text draft content while the submission is open for editing.
    /// </summary>
    Task<SaveRetrospectiveDraftResponseDto> SaveDraft(
        Guid submissionId,
        SaveRetrospectiveDraftRequestDto request);

    /// <summary>
    /// Final submit: merges optional request text with the saved draft and sets status to <c>TurnedIn</c>.
    /// </summary>
    Task<RetrospectiveAttemptResponseDto> SubmitRetrospective(
        Guid submissionId,
        SubmitRetrospectiveRequestDto request);
}
