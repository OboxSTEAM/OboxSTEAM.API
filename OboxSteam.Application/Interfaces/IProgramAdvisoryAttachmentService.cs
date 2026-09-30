using Microsoft.AspNetCore.Http;
using OboxSteam.Application.DTOs.ProgramAdvisoryDTO;

namespace OboxSteam.Application.Interfaces;

public interface IProgramAdvisoryAttachmentService
{
    /// <summary>Uploads an unsent chat attachment; it is linked when a message references it.</summary>
    Task<AdvisoryDiscussionAttachmentDto> UploadAsync(Guid programId, IFormFile file);

    /// <summary>Short-lived download URL (15 minutes).</summary>
    Task<AdvisoryAttachmentUrlDto> GetUrlAsync(Guid programId, Guid attachmentId);

    /// <summary>Deletes unsent attachments older than 24 hours (S3 object and row). Returns the row count.</summary>
    Task<int> PurgeUnsentAsync(CancellationToken cancellationToken = default);
}
