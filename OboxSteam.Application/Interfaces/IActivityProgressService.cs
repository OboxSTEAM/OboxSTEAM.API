using OboxSteam.Application.DTOs.ActivityProgressDTO;
using OboxSteam.Domain.Enums;

namespace OboxSteam.Application.Interfaces;

public interface IActivityProgressService
{
    Task<ActivityProgressResponseDto> StartActivityProgressAsync(CreateActivityProgressRequestDto request);

    Task<ActivityProgressResponseDto> UpdateActivityProgressAsync(UpdateActivityProgressRequestDto request);

    Task<ActivityProgressResponseDto> CompleteActivityForModuleEnrollmentAsync(
        Guid moduleEnrollmentId,
        Guid activityId,
        Guid studentId,
        CompletionSource? completionSource = null);

    /// <summary>
    /// Test-only: forces an activity to Done for a student, bypassing all business
    /// rules. Resolves the module enrollment automatically from the activity module
    /// and the student's latest attempt, then recalculates module/program progress.
    /// </summary>
    Task<ActivityProgressResponseDto> ForceCompleteActivityAsync(Guid studentId, Guid activityId);

    /// <summary>
    /// Mentor/Manager: marks a LiveOnline/Offline session activity Done for every
    /// active student on the class roster who has Present/Late/Excused attendance
    /// for the given class session. Per-student outcomes are returned; the batch
    /// never fails wholesale.
    /// </summary>
    Task<MentorCompleteBulkResponseDto> MentorCompleteClassSessionAsync(MentorCompleteBulkRequestDto request);

    Task<ActivityProgressResponseDto> SaveCheckpointForModuleEnrollmentAsync(
        Guid moduleEnrollmentId,
        Guid activityId,
        Guid studentId,
        string resumeStateJson);
}
