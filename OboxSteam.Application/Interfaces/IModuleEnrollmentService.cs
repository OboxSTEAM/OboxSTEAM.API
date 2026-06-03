using OboxSteam.Application.Commons;
using OboxSteam.Application.DTOs.EnrollmentDTO;

namespace OboxSteam.Application.Interfaces;

public interface IModuleEnrollmentService
{
    Task<ModuleEnrollmentResponseDto> EnrollModuleAsync(CreateModuleEnrollmentRequestDto request);

    Task<ModuleEnrollmentResponseDto> RetakeModuleAsync(UpdateModuleEnrollmentRequestDto request);

    Task<ModuleEnrollmentResponseDto> GetModuleEnrollmentByIdAsync(Guid id);

    Task<Pagination<ModuleEnrollmentResponseDto>> GetModuleEnrollmentsByProgramEnrollmentAsync(
        Guid programEnrollmentId,
        string? sortBy,
        bool isDescending,
        int page,
        int pageSize);
}
