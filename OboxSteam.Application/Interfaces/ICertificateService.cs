using OboxSteam.Application.DTOs.CertificateDTO;

namespace OboxSteam.Application.Interfaces;

public interface ICertificateService
{
    /// <summary>
    /// Issues a program certificate when all activities are Done. Idempotent.
    /// Returns null when the enrollment is not yet eligible.
    /// </summary>
    Task<CertificateDetailDto?> EnsureProgramCertificateAsync(Guid programEnrollmentId);

    /// <summary>
    /// Seed/system variant of <see cref="EnsureProgramCertificateAsync"/> that skips caller auth.
    /// Still requires all program activities to be Done. Idempotent.
    /// </summary>
    Task<CertificateDetailDto?> EnsureProgramCertificateForSeedAsync(Guid programEnrollmentId);

    Task<List<CertificateListItemDto>> GetMyCertificatesAsync();

    Task<CertificateDetailDto> GetCertificateByIdAsync(Guid id);

    Task<CertificateDetailDto?> GetCertificateByEnrollmentAsync(Guid programEnrollmentId);

    Task<CertificateDetailDto> GetCertificateByCodeAsync(string code);
}
