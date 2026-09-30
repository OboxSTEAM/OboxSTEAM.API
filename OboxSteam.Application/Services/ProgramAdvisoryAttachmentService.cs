using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using OboxSteam.Application.Commons;
using OboxSteam.Application.DTOs.ProgramAdvisoryDTO;
using OboxSteam.Application.Interfaces;
using OboxSteam.Application.Utils;
using OboxSteam.Domain.Entities;
using OboxSteam.Domain.Interfaces;

namespace OboxSteam.Application.Services;

public sealed class ProgramAdvisoryAttachmentService : IProgramAdvisoryAttachmentService
{
    private const int MaxFileNameLength = 255;
    private const int MaxStorageNameLength = 200;
    private static readonly TimeSpan UrlLifetime = TimeSpan.FromMinutes(15);
    private static readonly TimeSpan UnsentRetention = TimeSpan.FromHours(24);
    private static readonly Regex UnsafeKeyCharacters = new("[^A-Za-z0-9._-]", RegexOptions.Compiled);

    private readonly IUnitOfWork _unitOfWork;
    private readonly IClaimsService _claimsService;
    private readonly ICurrentTime _currentTime;
    private readonly IBlobService _blobService;
    private readonly ILogger<ProgramAdvisoryAttachmentService> _logger;

    public ProgramAdvisoryAttachmentService(
        IUnitOfWork unitOfWork,
        IClaimsService claimsService,
        ICurrentTime currentTime,
        IBlobService blobService,
        ILogger<ProgramAdvisoryAttachmentService> logger)
    {
        _unitOfWork = unitOfWork;
        _claimsService = claimsService;
        _currentTime = currentTime;
        _blobService = blobService;
        _logger = logger;
    }

    public async Task<AdvisoryDiscussionAttachmentDto> UploadAsync(Guid programId, IFormFile file)
    {
        var participant = await AdvisoryParticipantAccess.RequireAsync(_unitOfWork, _claimsService, programId);
        if (file == null || file.Length == 0)
        {
            throw ErrorHelper.BadRequest("A non-empty file is required.", "ATTACHMENT_INVALID");
        }

        if (file.Length > DiscussionAttachmentFileRules.MaxSizeBytes)
        {
            throw ErrorHelper.BadRequest("Attachments must be at most 20 MB.", "ATTACHMENT_TOO_LARGE");
        }

        var fileName = Path.GetFileName(file.FileName ?? string.Empty).Trim();
        if (fileName.Length == 0
            || !DiscussionAttachmentFileRules.TryResolve(fileName, file.ContentType, out var kind, out var contentType))
        {
            throw ErrorHelper.BadRequest(
                "Allowed attachments: png, jpg, jpeg, gif, webp, pdf, doc, docx, ppt, pptx, xls, xlsx, zip.",
                "ATTACHMENT_TYPE_NOT_ALLOWED");
        }

        fileName = LimitFileName(fileName);
        var attachmentId = Guid.NewGuid();
        var folder = $"advisory/{programId:D}/{attachmentId:D}";
        var storageName = StorageName(fileName);
        await using (var stream = file.OpenReadStream())
        {
            await _blobService.UploadFileAsync(storageName, stream, folder);
        }

        var now = _currentTime.GetCurrentTime().ToUniversalTime();
        var attachment = new ProgramAdvisoryDiscussionAttachment
        {
            Id = attachmentId,
            ProgramId = programId,
            UploaderUserId = participant.User.Id,
            FileName = fileName,
            ContentType = contentType,
            SizeBytes = file.Length,
            Kind = kind,
            StorageKey = $"{folder}/{storageName}",
            CreatedAt = now,
            CreatedBy = participant.User.Id,
        };
        await _unitOfWork.ProgramAdvisoryDiscussionAttachments.AddAsync(attachment);
        await _unitOfWork.SaveChangesAsync();
        return ProgramAdvisoryDiscussionService.MapAttachment(attachment);
    }

    public async Task<AdvisoryAttachmentUrlDto> GetUrlAsync(Guid programId, Guid attachmentId)
    {
        var participant = await AdvisoryParticipantAccess.RequireAsync(_unitOfWork, _claimsService, programId);
        var attachment = await _unitOfWork.ProgramAdvisoryDiscussionAttachments.GetByIdAsync(attachmentId);
        if (attachment == null
            || attachment.IsDeleted
            || attachment.ProgramId != programId
            || !await IsVisibleAsync(attachment, participant.User.Id))
        {
            throw ErrorHelper.NotFound($"Attachment '{attachmentId}' was not found.");
        }

        var url = await _blobService.GetFileUrlAsync(attachment.StorageKey, UrlLifetime);
        if (string.IsNullOrWhiteSpace(url))
        {
            throw ErrorHelper.Internal("Could not create a download link for this attachment.");
        }

        return new AdvisoryAttachmentUrlDto
        {
            Url = url,
            ExpiresAt = _currentTime.GetCurrentTime().ToUniversalTime().Add(UrlLifetime),
        };
    }

    public async Task<int> PurgeUnsentAsync(CancellationToken cancellationToken = default)
    {
        var cutoff = _currentTime.GetCurrentTime().ToUniversalTime().Subtract(UnsentRetention);
        var stale = await _unitOfWork.ProgramAdvisoryDiscussionAttachments.GetAllAsync(
            a => a.MessageId == null && a.CreatedAt < cutoff && !a.IsDeleted);
        if (stale.Count == 0)
        {
            return 0;
        }

        foreach (var attachment in stale)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                await _blobService.DeleteByKeyAsync(attachment.StorageKey, cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(ex, "Could not delete unsent attachment object {StorageKey}.", attachment.StorageKey);
            }
        }

        await _unitOfWork.ProgramAdvisoryDiscussionAttachments.HardRemoveRange(stale);
        await _unitOfWork.SaveChangesAsync();
        _logger.LogInformation("Purged {Count} unsent discussion attachments.", stale.Count);
        return stale.Count;
    }

    /// <summary>Unsent uploads are private to the uploader; attachments of deleted messages are hidden.</summary>
    private async Task<bool> IsVisibleAsync(ProgramAdvisoryDiscussionAttachment attachment, Guid userId)
    {
        if (!attachment.MessageId.HasValue)
        {
            return attachment.UploaderUserId == userId;
        }

        var message = await _unitOfWork.ProgramAdvisoryDiscussionMessages.GetByIdAsync(attachment.MessageId.Value);
        return message != null && !message.IsDeleted && message.RemovedAt == null;
    }

    private static string LimitFileName(string fileName)
    {
        if (fileName.Length <= MaxFileNameLength)
        {
            return fileName;
        }

        var extension = Path.GetExtension(fileName);
        return fileName[..(MaxFileNameLength - extension.Length)] + extension;
    }

    private static string StorageName(string fileName)
    {
        var safe = UnsafeKeyCharacters.Replace(fileName, "_");
        if (safe.Length <= MaxStorageNameLength)
        {
            return safe;
        }

        var extension = Path.GetExtension(safe);
        return safe[..(MaxStorageNameLength - extension.Length)] + extension;
    }
}
