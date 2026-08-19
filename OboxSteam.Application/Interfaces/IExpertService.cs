using OboxSteam.Application.Commons;
using OboxSteam.Application.DTOs.ExpertDTO;

namespace OboxSteam.Application.Interfaces;

public interface IExpertService
{
    Task<ExpertResponseDto> GetExpertByIdAsync(Guid id);
    Task<Pagination<ExpertResponseDto>> GetAllExpertsAsync(
        string? search,
        string? sortBy,
        bool isDescending,
        int page,
        int pageSize,
        string? code = null);
    Task<ExpertResponseDto> AddExpertAsync(ExpertCreateDto expertCreateDto);
    Task<ExpertProgramSummaryDto> AddProgramToExpertAsync(Guid expertId, Guid programId, AddProgramToExpertDto? dto = null);
    Task<ExpertResponseDto> UpdateExpertAsync(Guid id, ExpertUpdateDto expertUpdateDto);
    Task<ExpertProgramSummaryDto> UpdateProgramOfExpertAsync(Guid expertId, Guid programId);
    Task<bool> DeleteExpertAsync(Guid id);
    Task<bool> RemoveProgramFromExpertAsync(Guid expertId, Guid programId);
    Task<ExpertDegreeResponseDto> AddDegreeAsync(Guid expertId, ExpertDegreeRequestDto dto);
    Task<ExpertDegreeResponseDto> UpdateDegreeAsync(Guid expertId, Guid degreeId, ExpertDegreeRequestDto dto);
    Task<bool> DeleteDegreeAsync(Guid expertId, Guid degreeId);
    Task<ExpertPublicationResponseDto> AddPublicationAsync(Guid expertId, ExpertPublicationRequestDto dto);
    Task<ExpertPublicationResponseDto> UpdatePublicationAsync(Guid expertId, Guid publicationId, ExpertPublicationRequestDto dto);
    Task<bool> DeletePublicationAsync(Guid expertId, Guid publicationId);
}
