using OboxSteam.Application.Commons;
using OboxSteam.Application.DTOs.ActivityDTO;
using OboxSteam.Domain.Enums;

namespace OboxSteam.Application.Interfaces;

public interface IActivityService
{
    Task<Pagination<ActivitiesResponseDto>> GetAllActivitiesAsync(
        string? search,
        string? sortBy,
        bool isDescending,
        int page,
        int pageSize,
        string? code,
        Guid? courseId,
        ActivityType? activityType);

    Task<ActivitiesResponseDto?> GetActivityByIdAsync(Guid activityId);

    Task<ActivitiesResponseDto?> GetActivityByCodeAsync(string code);

    Task<ActivitiesResponseDto> CreateActivityAsync(CreateActivitiesRequestDto request);

    Task<ActivitiesResponseDto?> UpdateActivityAsync(Guid activityId, UpdateActivitiesRequestDto request);

    Task<bool> DeleteActivityAsync(Guid activityId);
}
