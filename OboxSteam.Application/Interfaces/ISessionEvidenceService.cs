using OboxSteam.Application.DTOs.MediaDTO;

namespace OboxSteam.Application.Interfaces;

/// <summary>Mentor field-evidence photos for Offline sessions (no face recognition).</summary>
public interface ISessionEvidenceService
{
    Task<MediaAssetDto> UploadEvidenceAsync(Guid classSessionId, Microsoft.AspNetCore.Http.IFormFile file);

    Task<IReadOnlyList<MediaAssetDto>> ListEvidenceAsync(Guid classSessionId);

    Task DeleteEvidenceAsync(Guid classSessionId, Guid mediaId);
}
