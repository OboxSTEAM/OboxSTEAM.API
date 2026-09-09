using OboxSteam.Application.Commons;
using OboxSteam.Application.DTOs.ProgramFrameworkDTO;
using OboxSteam.Domain.Enums;

namespace OboxSteam.Application.Interfaces;

public interface IProgramFrameworkService
{
    Task<Pagination<ProgramFrameworkResponseDto>> GetFrameworksAsync(
        string? search,
        ProgramCategory? category,
        int page,
        int pageSize);

    Task<ProgramFrameworkResponseDto> GetFrameworkByIdAsync(Guid id);

    Task<ProgramFrameworkResponseDto> CreateFrameworkAsync(CreateProgramFrameworkRequest request);

    Task<ProgramFrameworkResponseDto> UpdateFrameworkAsync(Guid id, UpdateProgramFrameworkRequest request);

    Task<bool> DeleteFrameworkAsync(Guid id);

    Task<ProgramFrameworkResponseDto> ArchiveFrameworkAsync(Guid id);

    Task<IReadOnlyList<ProgramFrameworkVersionResponseDto>> GetVersionsAsync(Guid frameworkId);

    Task<ProgramFrameworkVersionResponseDto> GetVersionAsync(Guid frameworkId, Guid versionId);

    Task<ProgramFrameworkVersionResponseDto> CreateDraftVersionAsync(Guid frameworkId);

    Task<ProgramFrameworkVersionResponseDto> PublishDraftVersionAsync(Guid frameworkId, Guid versionId);

    Task<ProgramFrameworkVersionResponseDto> SaveDraftRubricAsync(
        Guid frameworkId,
        Guid versionId,
        SaveFrameworkRubricRequest request);

    Task<FrameworkRubricCriterionResponseDto> AddCriterionAsync(
        Guid frameworkId,
        FrameworkRubricCriterionRequest request);

    Task<FrameworkRubricCriterionResponseDto> UpdateCriterionAsync(
        Guid frameworkId,
        Guid criterionId,
        FrameworkRubricCriterionRequest request);

    Task<bool> DeleteCriterionAsync(Guid frameworkId, Guid criterionId);
}
