using OboxSteam.Application.DTOs.ActivityProgressDTO;

namespace OboxSteam.Application.Interfaces;

public interface IActivityProgressService
{
    Task<ActivityProgressResponseDto> StartActivityProgressAsync(CreateActivityProgressRequestDto request);

    Task<ActivityProgressResponseDto> UpdateActivityProgressAsync(UpdateActivityProgressRequestDto request);

    Task<ActivityProgressResponseDto> CompleteActivityForModuleEnrollmentAsync(
        Guid moduleEnrollmentId,
        Guid activityId,
        Guid studentId);
}
