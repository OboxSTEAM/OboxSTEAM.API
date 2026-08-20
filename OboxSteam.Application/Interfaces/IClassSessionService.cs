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

    Task<ClassSessionWithStudentsResponseDto> GetClassSessionWithStudentsAsync(Guid id);

    Task<ClassSessionResponseDto> CreateClassSessionAsync(CreateClassSessionRequestDto request);

    /// <summary>
    /// Bulk-generates sessions from the program curriculum using a weekly repeat pattern.
    /// LiveOnline/Offline activities (module order → course → ActivityOrder) and assignments
    /// fill consecutive weekly slots from the class start date. All-or-nothing: any mentor
    /// overlap or out-of-range slot fails the whole generation before anything is saved.
    /// </summary>
    Task<List<ClassSessionResponseDto>> GenerateClassSessionsAsync(
        Guid classId,
        GenerateClassSessionsRequestDto request);

    Task<ClassSessionResponseDto> UpdateClassSessionAsync(Guid id, UpdateClassSessionRequestDto request);

    Task<bool> DeleteClassSessionAsync(Guid id);
}
