using OboxSteam.Application.DTOs.CertificateDTO;

namespace OboxSteam.Application.Interfaces;

public interface ICertificateService
{
    /// <summary>
    /// Issues a program certificate when all activities are Done. Idempotent.
    /// Requires the caller to be the enrollment student, Admin, or Manager.
    /// Returns null when the enrollment is not yet eligible.
    /// </summary>
    Task<CertificateDetailDto?> EnsureProgramCertificateAsync(Guid programEnrollmentId);

    /// <summary>
    /// System/auto-issue variant used after progress or grading updates.
    /// Skips caller auth so mentor grading / session complete can still issue.
    /// Still requires all program activities to be Done. Idempotent.
    /// </summary>
    Task<CertificateDetailDto?> EnsureProgramCertificateInternalAsync(Guid programEnrollmentId);

    /// <summary>
    /// Seed variant of <see cref="EnsureProgramCertificateInternalAsync"/>.
    /// </summary>
    Task<CertificateDetailDto?> EnsureProgramCertificateForSeedAsync(Guid programEnrollmentId);

    /// <summary>
    /// Issues a pathway certificate when every bundle item program enrollment is Completed.
    /// Idempotent. Skips caller auth (system/auto-issue after program completion).
    /// Returns null when the bundle is not yet eligible.
    /// </summary>
    Task<CertificateDetailDto?> EnsureBundleCertificateInternalAsync(Guid bundleEnrollmentId);

    Task<List<CertificateListItemDto>> GetMyCertificatesAsync();

    Task<CertificateDetailDto> GetCertificateByIdAsync(Guid id);

    Task<CertificateDetailDto?> GetCertificateByEnrollmentAsync(Guid programEnrollmentId);

    Task<CertificateDetailDto> GetCertificateByCodeAsync(string code);
}
