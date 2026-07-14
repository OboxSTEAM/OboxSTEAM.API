using OboxSteam.Application.DTOs.CertificateDTO;

namespace OboxSteam.Application.Interfaces;

public interface ICertificateService
{
    /// <summary>
    /// Issues a program certificate when all activities are Done. Idempotent.
    /// Returns null when the enrollment is not yet eligible.
    /// </summary>
    Task<CertificateDetailDto?> EnsureProgramCertificateAsync(Guid programEnrollmentId);

    Task<List<CertificateListItemDto>> GetMyCertificatesAsync();

    Task<CertificateDetailDto> GetCertificateByIdAsync(Guid id);

    Task<CertificateDetailDto?> GetCertificateByEnrollmentAsync(Guid programEnrollmentId);

    Task<CertificateDetailDto> GetCertificateByCodeAsync(string code);
}
