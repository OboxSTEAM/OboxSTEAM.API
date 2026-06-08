using OboxSteam.Application.Commons;
using OboxSteam.Application.DTOs.ProgramDTO;
using OboxSteam.Domain.Enums;

namespace OboxSteam.Application.Interfaces;

public interface IProgramService
{
    Task<ProgramsResponseDto> GetProgramByIdAsync(Guid id);
    Task<ProgramsResponseDto> GetProgramByNameAsync(string name);
    Task<Pagination<ProgramListItemDto>> GetAllProgramsAsync(
        string? search,
        string? sortBy,
        bool isDescending,
        int page,
        int pageSize,
        string? code = null,
        DifficultyLevel? level = null,
        decimal? rating = null,
        string? skillsGained = null,
        string? status = null,
        string? category = null);

    Task<Pagination<ProgramsResponseDto>> GetAllProgramsWithModulesAsync(
        string? search,
        string? sortBy,
        bool isDescending,
        int page,
        int pageSize,
        string? code = null,
        DifficultyLevel? level = null,
        decimal? rating = null,
        string? skillsGained = null,
        string? status = null,
        string? category = null);

    Task<ProgramsResponseDto> CreateProgramAsync(CreateProgramRequestDto request);


    Task<ProgramsResponseDto> UpdateProgramAsync(Guid id, UpdateProgramRequestDto request);

    Task<bool> DeleteProgramAsync(Guid id);
}
