using OboxSteam.Application.Commons;
using OboxSteam.Application.DTOs.ProgramDTO;
using OboxSteam.Domain.Enums;

namespace OboxSteam.Application.Interfaces;

public interface IProgramService
{
    Task<ProgramResponseDto> GetProgramByIdAsync(Guid id);
    Task<ProgramResponseDto> GetProgramByNameAsync(string name);
    Task<Pagination<ProgramResponseDto>> GetAllProgramAsync(
        string? search,
        string? sortBy,
        bool isDescending,
        int page,
        int pageSize,
        string? code = null,
        DifficultyLevel? level = null,
        decimal? rating = null,
        string? skillsGained = null,
        string? status = null);

    Task<ProgramResponseDto> AddProgramAsync(ProgramCreateDto programCreateDto);

    Task<ProgramResponseDto> UpdateProgramAsync(Guid id, ProgramUpdateDto programUpdateDto);

    Task<bool> DeleteProgramAsync(Guid id);
}
