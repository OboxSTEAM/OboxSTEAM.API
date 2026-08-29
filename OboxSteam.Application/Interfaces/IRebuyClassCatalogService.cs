using OboxSteam.Application.DTOs.ClassDTO;

namespace OboxSteam.Application.Interfaces;

public interface IRebuyClassCatalogService
{
    /// <summary>
    /// Open or InProgress Standard classes the current student may consider for a rebuy of
    /// <paramref name="programId"/>, with per-module session progress and eligibility.
    /// </summary>
    Task<RebuyClassCatalogDto> GetRebuyClassesAsync(Guid programId);
}
