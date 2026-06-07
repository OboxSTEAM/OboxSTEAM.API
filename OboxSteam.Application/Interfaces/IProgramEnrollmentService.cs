using OboxSteam.Application.Commons;
using OboxSteam.Application.DTOs.EnrollmentDTO;

namespace OboxSteam.Application.Interfaces;

public interface IProgramEnrollmentService
{
    Task<ProgramEnrollmentResponseDto> EnrollProgramAsync(CreateEnrollmentProgramRequestDto request);

    Task<ProgramEnrollmentResponseDto> GetProgramEnrollmentByIdAsync(Guid id);

    Task<Pagination<ProgramEnrollmentResponseDto>> GetMyProgramEnrollmentsAsync(
        string? sortBy,
        bool isDescending,
        int page,
        int pageSize);

    Task<Pagination<ProgramEnrollmentResponseDto>> GetProgramEnrollmentsByStudentIdAsync(
        Guid studentId,
        string? sortBy,
        bool isDescending,
        int page,
        int pageSize);
}
