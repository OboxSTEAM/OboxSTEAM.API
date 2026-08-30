using OboxSteam.Application.DTOs.ClassDTO;

namespace OboxSteam.Application.Interfaces;

public interface IRebuyClassCatalogService
{
    /// <summary>
    /// Open-only for first purchase and Completed retakes; Open or InProgress with stop-module
    /// eligibility after Failed/Dropped.
    /// </summary>
    Task<RebuyClassCatalogDto> GetRebuyClassesAsync(Guid programId);
}
