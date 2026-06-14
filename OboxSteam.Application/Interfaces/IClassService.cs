using OboxSteam.Application.Commons;
using OboxSteam.Application.DTOs.ClassDTO;
using OboxSteam.Domain.Enums;

namespace OboxSteam.Application.Interfaces;

public interface IClassService
{
    Task<Pagination<ClassResponseDto>> GetAllClassesAsync(
        string? search,
        string? sortBy,
        bool isDescending,
        int page,
        int pageSize,
        Guid? programId = null,
        ClassStatus? status = null,
        Guid? mentorId = null);

    Task<ClassResponseDto> GetClassByIdAsync(Guid id);

    Task<ClassResponseDto> CreateClassAsync(CreateClassRequestDto request);

    Task<ClassResponseDto> UpdateClassAsync(Guid id, UpdateClassRequestDto request);

    Task<ClassResponseDto> OpenClassAsync(Guid id);

    Task<ClassResponseDto> StartClassAsync(Guid id);

    Task<ClassResponseDto> CompleteClassAsync(Guid id);

    /// <summary>
    /// Transitions a single Open class to InProgress when it is full and StartDate has passed.
    /// </summary>
    Task TryAutoStartClassIfReadyAsync(Guid classId);

    /// <summary>
    /// Scans all eligible Open classes and transitions them to InProgress when ready.
    /// </summary>
    Task<int> AutoStartEligibleOpenClassesAsync();
}
