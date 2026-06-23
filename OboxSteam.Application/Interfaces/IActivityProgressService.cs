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

    Task<ActivityProgressResponseDto> SaveCheckpointForModuleEnrollmentAsync(
        Guid moduleEnrollmentId,
        Guid activityId,
        Guid studentId,
        string resumeStateJson);
}
