using OboxSteam.Application.DTOs.ParentProgressionDTO;

namespace OboxSteam.Application.Interfaces;

public interface IParentProgressionService
{
    Task<ParentChildProgressionDto> GetChildProgressionAsync(Guid studentId);

    Task<ParentEnrollmentProgressionDto> GetEnrollmentProgressionAsync(Guid studentId, Guid enrollmentId);
}
