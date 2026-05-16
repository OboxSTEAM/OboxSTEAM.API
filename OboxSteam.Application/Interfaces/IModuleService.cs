using OboxSteam.Application.Commons;
using OboxSteam.Application.DTOs.ModuleDTO;
using OboxSteam.Domain.Enums;

namespace OboxSteam.Application.Interfaces
{
    public interface IModuleService
    {
        Task<ModuleResponseDto> GetModuleByIdAsync(Guid id);
        Task<ModuleResponseDto> GetModuleByNameAsync(string name);
        Task<Pagination<ModuleResponseDto>> GetAllModulesAsync(
            string? search,
            string? sortBy,
            bool isDescending,
            int page,
            int pageSize,
            ModuleType? moduleType);

        Task<ModuleResponseDto> AddModuleAsync(ModuleCreateDto moduleCreateDto);

        Task<ModuleResponseDto> UpdateModuleAsync(Guid id, ModuleUpdateDto moduleUpdateDto);

        Task<bool> DeleteModuleAsync(Guid id);
    }
}
