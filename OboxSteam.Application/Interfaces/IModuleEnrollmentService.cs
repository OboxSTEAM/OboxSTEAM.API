using OboxSteam.Application.Commons;
using OboxSteam.Application.DTOs.EnrollmentDTO;

namespace OboxSteam.Application.Interfaces;

public interface IModuleEnrollmentService
{
    Task<ModuleEnrollmentResponseDto> RetakeModuleAsync(UpdateModuleEnrollmentRequestDto request);

    Task<ModuleEnrollmentResponseDto> GetModuleEnrollmentByIdAsync(Guid id);
}
