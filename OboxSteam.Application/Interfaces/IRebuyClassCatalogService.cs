using OboxSteam.Application.DTOs.ClassDTO;
using OboxSteam.Domain.Entities;

namespace OboxSteam.Application.Interfaces;

public interface IRebuyClassCatalogService
{
    /// <summary>
    /// Open-only for first purchase, Completed retakes, and Failed/Dropped after the
    /// window; Open or InProgress with stop-module eligibility after Failed/Dropped
    /// inside the window. Conflicts when the student still has an Active enrollment.
    /// </summary>
    Task<RebuyClassCatalogDto> GetRebuyClassesAsync(Guid programId);

    /// <summary>
    /// Continuity picker while the program enrollment is still Active (voluntary or
    /// post-recovery module retake). Same <see cref="RebuyClassCatalogDto"/> shape as rebuy.
    /// </summary>
    Task<RebuyClassCatalogDto> GetContinuityClassesForModuleEnrollmentAsync(Guid moduleEnrollmentId);

    /// <summary>Builds an Active-redelivery catalog for an already-validated module/program pair.</summary>
    Task<RebuyClassCatalogDto> BuildActiveCatalogAsync(
        Guid studentId,
        Program program,
        ProgramEnrollment programEnrollment,
        Module stopModule);
}
