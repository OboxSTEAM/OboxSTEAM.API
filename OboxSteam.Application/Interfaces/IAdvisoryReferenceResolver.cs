using OboxSteam.Application.DTOs.ProgramAdvisoryDTO;
using OboxSteam.Domain.Entities;

namespace OboxSteam.Application.Interfaces;

public interface IAdvisoryReferenceResolver
{
    Task<ProgramAdvisoryReference> CaptureAsync(
        Program program,
        User actor,
        CreateAdvisoryReferenceRequest request);

    Task<AdvisoryReferenceDto> ResolveAsync(Guid programId, Guid referenceId);

    Task<IReadOnlyList<AdvisoryReferenceDto>> ResolveManyAsync(
        Guid programId,
        IReadOnlyList<Guid> referenceIds);
}
