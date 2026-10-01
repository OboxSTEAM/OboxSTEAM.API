using OboxSteam.Application.DTOs.ProgramAdvisoryDTO;
using OboxSteam.Domain.Entities;

namespace OboxSteam.Application.Interfaces;

public interface IAdvisoryReferenceResolver
{
    /// <summary>Resolves already-loaded references, loading the curriculum tree at most once.</summary>
    Task<IReadOnlyDictionary<Guid, AdvisoryReferenceDto>> ResolveLoadedAsync(
        Guid programId,
        IReadOnlyCollection<ProgramAdvisoryReference> references);
}
