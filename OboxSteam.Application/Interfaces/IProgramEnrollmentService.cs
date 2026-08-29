using OboxSteam.Domain.Entities;
using OboxSteam.Application.DTOs.EnrollmentDTO;
using OboxSteam.Application.Commons;

namespace OboxSteam.Application.Interfaces;

public interface IProgramEnrollmentService
{
    Task<ProgramEnrollment> GetOrCreatePendingEnrollmentAsync(Guid studentId, Guid programId);

    Task<ProgramEnrollmentResponseDto> GetProgramEnrollmentByIdAsync(Guid id);

    Task<Pagination<ProgramEnrollmentResponseDto>> GetMyProgramEnrollmentsAsync(
        Guid? programId,
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

    Task<ProgramEnrollmentClassDto> GetProgramEnrollmentClassAsync(Guid enrollmentId);

    Task<ProgramEnrollmentResponseDto> WithdrawAsync(Guid enrollmentId);
}
