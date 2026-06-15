using OboxSteam.Application.Commons;
using OboxSteam.Application.DTOs.ClassSessionDTO;
using OboxSteam.Domain.Enums;

namespace OboxSteam.Application.Interfaces;

public interface IClassSessionService
{
    Task<Pagination<ClassSessionResponseDto>> GetClassSessionsByClassIdAsync(
        Guid classId,
        string? sortBy,
        bool isDescending,
        int page,
        int pageSize,
        Guid? moduleId = null,
        SessionKind? sessionKind = null,
        ClassSessionStatus? status = null,
        DateTime? from = null,
        DateTime? to = null);

    Task<ClassSessionResponseDto> GetClassSessionByIdAsync(Guid id);

    Task<ClassSessionResponseDto> CreateClassSessionAsync(CreateClassSessionRequestDto request);

    Task<ClassSessionResponseDto> UpdateClassSessionAsync(Guid id, UpdateClassSessionRequestDto request);

    Task<bool> DeleteClassSessionAsync(Guid id);
}
