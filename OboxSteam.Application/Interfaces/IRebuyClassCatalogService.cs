using OboxSteam.Application.DTOs.ClassDTO;

namespace OboxSteam.Application.Interfaces;

public interface IRebuyClassCatalogService
{
    /// <summary>
    /// Open-only for first purchase, Completed retakes, and Failed/Dropped after the
    /// window; Open or InProgress with stop-module eligibility after Failed/Dropped
    /// inside the window.
    /// </summary>
    Task<RebuyClassCatalogDto> GetRebuyClassesAsync(Guid programId);
}
