using OboxSteam.Application.Commons;
using OboxSteam.Application.DTOs.ModuleDTO;
using OboxSteam.Domain.Enums;

namespace OboxSteam.Application.Interfaces;

public interface IModuleService
{
    Task<ModulesResponseDto> GetModuleByIdAsync(Guid id);
    Task<ModulesResponseDto> GetModuleByNameAsync(string name);
    Task<Pagination<ModulesResponseDto>> GetAllModulesAsync(
        string? search,
        string? sortBy,
        bool isDescending,
        int page,
        int pageSize,
        string? code,
        ModuleType? moduleType);

    Task<ModulesResponseDto> CreateModuleAsync(CreateModuleRequestDto request);

    Task<ModulesResponseDto> UpdateModuleAsync(Guid id, UpdateModuleRequestDto request);

    Task<bool> DeleteModuleAsync(Guid id);
}
