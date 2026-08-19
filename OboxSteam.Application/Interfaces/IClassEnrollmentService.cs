using OboxSteam.Application.Commons;
using OboxSteam.Application.DTOs.ClassEnrollmentDTO;

namespace OboxSteam.Application.Interfaces;

public interface IClassEnrollmentService
{
    Task<ClassEnrollmentResponseDto> EnrollClassAsync(CreateClassEnrollmentRequestDto request);

    Task<ClassEnrollmentResponseDto> TransferClassAsync(Guid id, UpdateClassEnrollmentRequestDto request);

    Task<ClassEnrollmentResponseDto> TransferClassByManagerAsync(Guid id, ManagerTransferClassRequestDto request);

    Task<ClassEnrollmentResponseDto> GetClassEnrollmentByIdAsync(Guid id);

    Task<Pagination<ClassEnrollmentResponseDto>> GetClassEnrollmentsByProgramEnrollmentAsync(
        Guid programEnrollmentId,
        string? sortBy,
        bool isDescending,
        int page,
        int pageSize);

    Task<List<StudentScheduleIntervalDto>> GetMyScheduleAsync();
}
