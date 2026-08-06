using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using OboxSteam.Application.DTOs.PortfolioDTO;
using OboxSteam.Application.Interfaces;
using OboxSteam.Application.Utils;
using OboxSteam.Application.Validation;
using OboxSteam.Domain.Entities;
using OboxSteam.Domain.Enums;
using OboxSteam.Domain.Interfaces;

namespace OboxSteam.Application.Services;

public sealed class PortfolioService : IPortfolioService
{
    private const int MaxGalleryAssetsPerOwner = 20;
    private const int MaxImageBytes = 5 * 1024 * 1024;

    private static readonly HashSet<PortfolioItemType> ManualCreatableTypes =
    [
        PortfolioItemType.ExternalCert,
        PortfolioItemType.Hobby,
        PortfolioItemType.Extracurricular,
        PortfolioItemType.Project,
    ];

    private static readonly HashSet<PortfolioItemType> AutoImportedTypes =
    [
        PortfolioItemType.CapstoneProject,
        PortfolioItemType.InternalCertificate,
        PortfolioItemType.HighlightReel,
    ];

    private static readonly HashSet<PortfolioSectionKind> BuiltInSectionKinds =
    [
        PortfolioSectionKind.ProjectsGroup,
        PortfolioSectionKind.ActivitiesGroup,
        PortfolioSectionKind.LinksGroup,
    ];

    private static readonly HashSet<PortfolioSectionKind> CustomSectionKinds =
    [
        PortfolioSectionKind.RichText,
        PortfolioSectionKind.Gallery,
        PortfolioSectionKind.Embed,
    ];

    private static readonly Dictionary<string, string> AllowedImageExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        [".jpg"] = "image/jpeg",
        [".jpeg"] = "image/jpeg",
        [".png"] = "image/png",
    };

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        // PascalCase enum names match the API's global JsonStringEnumConverter.
        // Theme/span fields opt into camelCase via CamelCaseJsonStringEnumConverter attributes.
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly IUnitOfWork _unitOfWork;
    private readonly IClaimsService _claimsService;
    private readonly IBlobService _blobService;
    private readonly IPortfolioHtmlSanitizer _htmlSanitizer;
    private readonly ILogger<PortfolioService> _logger;

    public PortfolioService(
        IUnitOfWork unitOfWork,
        IClaimsService claimsService,
        IBlobService blobService,
        IPortfolioHtmlSanitizer htmlSanitizer,
        ILogger<PortfolioService> logger)
    {
        _unitOfWork = unitOfWork;
        _claimsService = claimsService;
        _blobService = blobService;
        _htmlSanitizer = htmlSanitizer;
        _logger = logger;
    }

    public async Task<PortfolioResponseDto> GetMyPortfolioAsync()
    {
        var student = await GetCurrentStudentAsync();
        var portfolio = await GetRootPortfolioForStudentOrThrowAsync(student.Id);
        await EnsureBuiltInSectionsAsync(portfolio);
        return await MapPortfolioResponseAsync(portfolio);
    }

    public async Task<PortfolioResponseDto> CreateMyPortfolioAsync()
    {
        var student = await GetCurrentStudentAsync();

        var existing = await _unitOfWork.Portfolios.FirstOrDefaultAsync(
            p => p.StudentId == student.Id && p.ParentPortfolioId == null && !p.IsDeleted);
        if (existing != null)
        {
            throw ErrorHelper.Conflict("You already have a portfolio.");
        }

        var portfolio = new Portfolio
        {
            Code = await GenerateUniquePortfolioCodeAsync(),
            StudentId = student.Id,
            IsPublic = false,
            Subdomain = null,
        };

        await _unitOfWork.Portfolios.AddAsync(portfolio);
        await _unitOfWork.SaveChangesAsync();

        await EnsureBuiltInSectionsAsync(portfolio);

        _logger.LogInformation(
            "[CreateMyPortfolioAsync] Portfolio {PortfolioId} created for student {StudentId}.",
            portfolio.Id,
            student.Id);

        return await MapPortfolioResponseAsync(portfolio);
    }

    public async Task<PortfolioResponseDto> UpdateMyPortfolioAsync(UpdatePortfolioRequestDto dto)
    {
        if (dto == null)
        {
            throw ErrorHelper.BadRequest("Portfolio update data is required.");
        }

        var student = await GetCurrentStudentAsync();
        var portfolio = await GetRootPortfolioForStudentOrThrowAsync(student.Id);

        if (dto.DisplayName != null)
        {
            portfolio.DisplayName = string.IsNullOrWhiteSpace(dto.DisplayName)
                ? null
                : dto.DisplayName.Trim();
        }

        if (dto.Headline != null)
        {
            portfolio.Headline = string.IsNullOrWhiteSpace(dto.Headline)
                ? null
                : dto.Headline.Trim();
        }

        if (dto.Tagline != null)
        {
            portfolio.Tagline = string.IsNullOrWhiteSpace(dto.Tagline)
                ? null
                : dto.Tagline.Trim();
        }

        if (dto.Summary != null)
        {
            portfolio.Summary = _htmlSanitizer.Sanitize(dto.Summary);
        }

        if (dto.AvatarUrl != null)
        {
            portfolio.AvatarUrl = string.IsNullOrWhiteSpace(dto.AvatarUrl)
                ? null
                : ValidateAndTrimOptionalUrl(dto.AvatarUrl, nameof(dto.AvatarUrl));
        }

        if (dto.CoverImageUrl != null)
        {
            portfolio.CoverImageUrl = string.IsNullOrWhiteSpace(dto.CoverImageUrl)
                ? null
                : ValidateAndTrimOptionalUrl(dto.CoverImageUrl, nameof(dto.CoverImageUrl));
        }

        if (dto.Theme != null)
        {
            PortfolioThemeValidator.ValidateTheme(dto.Theme);
            ApplyTheme(portfolio, dto.Theme);
        }

        if (dto.Links != null)
        {
            portfolio.Links = dto.Links.Count == 0
                ? null
                : JsonSerializer.Serialize(dto.Links, JsonOptions);
        }

        MarkDraftDirty(portfolio);
        await _unitOfWork.Portfolios.Update(portfolio);
        await _unitOfWork.SaveChangesAsync();

        return await MapPortfolioResponseAsync(portfolio);
    }

    public async Task<SubdomainAvailabilityResponseDto> CheckSubdomainAvailabilityAsync(string subdomain)
    {
        var student = await GetCurrentStudentAsync();
        var portfolio = await GetRootPortfolioForStudentOrThrowAsync(student.Id);
        return await BuildSubdomainAvailabilityAsync(subdomain, portfolio.Id);
    }

    public async Task<PortfolioResponseDto> UpdateMySubdomainAsync(
        UpdatePortfolioSubdomainRequestDto dto)
    {
        if (dto == null)
        {
            throw ErrorHelper.BadRequest("Subdomain update data is required.");
        }

        var student = await GetCurrentStudentAsync();
        var portfolio = await GetRootPortfolioForStudentOrThrowAsync(student.Id);

        await ApplySubdomainAsync(portfolio, dto.Subdomain);
        MarkDraftDirty(portfolio);
        await _unitOfWork.Portfolios.Update(portfolio);
        await _unitOfWork.SaveChangesAsync();

        return await MapPortfolioResponseAsync(portfolio);
    }

    public async Task<PortfolioResponseDto> UpdateMyPublicationAsync(
        UpdatePortfolioPublicationRequestDto dto)
    {
        if (dto == null || !dto.IsPublished.HasValue)
        {
            throw ErrorHelper.BadRequest("Publication state is required.");
        }

        var student = await GetCurrentStudentAsync();
        var portfolio = await GetRootPortfolioForStudentOrThrowAsync(student.Id);
        var isPublished = dto.IsPublished.Value;

        if (isPublished)
        {
            EnsureCanPublish(portfolio);

            var availability = await BuildSubdomainAvailabilityAsync(
                portfolio.Subdomain!,
                portfolio.Id);
            if (!availability.Available)
            {
                throw ErrorHelper.Conflict(
                    availability.Reason ?? "Subdomain is already taken.");
            }

            await EnsureBuiltInSectionsAsync(portfolio);

            var snapshot = await BuildPublicSnapshotAsync(portfolio);
            portfolio.PublishedSnapshot = JsonSerializer.Serialize(snapshot, JsonOptions);
            portfolio.LastPublishedAt = DateTime.UtcNow;
            portfolio.HasUnpublishedChanges = false;
            portfolio.IsPublic = true;
        }
        else
        {
            portfolio.IsPublic = false;
        }

        await _unitOfWork.Portfolios.Update(portfolio);
        await _unitOfWork.SaveChangesAsync();

        return await MapPortfolioResponseAsync(portfolio);
    }

    public async Task<PortfolioCustomItemResponseDto> AddItemAsync(CreatePortfolioItemRequestDto dto)
    {
        if (dto == null)
        {
            throw ErrorHelper.BadRequest("Portfolio item data is required.");
        }

        if (string.IsNullOrWhiteSpace(dto.Title))
        {
            throw ErrorHelper.BadRequest("Title is required.");
        }

        if (!ManualCreatableTypes.Contains(dto.ItemType))
        {
            throw ErrorHelper.BadRequest("This item type cannot be created manually.");
        }

        var student = await GetCurrentStudentAsync();
        var portfolio = await GetRootPortfolioForStudentOrThrowAsync(student.Id);
        var items = await GetPortfolioItemsAsync(portfolio.Id);

        var nextOrder = dto.DisplayOrder ?? (items.Count == 0 ? 0 : items.Max(i => i.DisplayOrder) + 1);
        if (nextOrder < 0)
        {
            throw ErrorHelper.BadRequest("DisplayOrder cannot be negative.");
        }

        var item = new PortfolioCustomItem
        {
            PortfolioId = portfolio.Id,
            ItemType = dto.ItemType,
            Title = dto.Title.Trim(),
            Subtitle = NormalizeOptional(dto.Subtitle),
            Organization = NormalizeOptional(dto.Organization),
            Description = _htmlSanitizer.Sanitize(dto.Description),
            StudentEditedBody = _htmlSanitizer.Sanitize(dto.StudentEditedBody),
            MediaUrl = NormalizeOptional(dto.MediaUrl),
            ExternalUrl = NormalizeOptional(dto.ExternalUrl),
            StartDate = dto.StartDate,
            EndDate = dto.EndDate,
            DisplayOrder = nextOrder,
            IsVisible = dto.IsVisible ?? true,
            IsFeatured = dto.IsFeatured,
            Span = dto.Span,
            Source = PortfolioItemSource.StudentEdited,
        };

        ApplyAccentColor(item, dto.AccentColor);

        await _unitOfWork.PortfolioCustomItems.AddAsync(item);
        await _unitOfWork.SaveChangesAsync();

        if (dto.MediaAssets != null)
        {
            await ReplaceMediaPlacementsAsync(portfolio.Id, item.Id, null, dto.MediaAssets);
        }

        MarkDraftDirty(portfolio);
        await _unitOfWork.Portfolios.Update(portfolio);
        await _unitOfWork.SaveChangesAsync();

        var appendixByItemId = await LoadAppendixByItemIdAsync([item.Id]);
        var mediaByItemId = await LoadItemMediaPlacementsAsync([item.Id]);
        return MapItemResponse(
            item,
            appendixByItemId.GetValueOrDefault(item.Id),
            mediaByItemId.GetValueOrDefault(item.Id));
    }

    public async Task<PortfolioCustomItemResponseDto> UpdateItemAsync(
        Guid itemId,
        UpdatePortfolioItemRequestDto dto)
    {
        if (itemId == Guid.Empty)
        {
            throw ErrorHelper.BadRequest("Portfolio item id is required.");
        }

        if (dto == null)
        {
            throw ErrorHelper.BadRequest("Portfolio item update data is required.");
        }

        var student = await GetCurrentStudentAsync();
        var portfolio = await GetRootPortfolioForStudentOrThrowAsync(student.Id);
        var item = await GetOwnedItemOrThrowAsync(portfolio.Id, itemId);

        if (dto.Title != null)
        {
            if (string.IsNullOrWhiteSpace(dto.Title))
            {
                throw ErrorHelper.BadRequest("Title cannot be empty.");
            }

            item.Title = dto.Title.Trim();
        }

        if (dto.Subtitle != null)
        {
            item.Subtitle = NormalizeOptional(dto.Subtitle);
        }

        if (dto.Organization != null)
        {
            item.Organization = NormalizeOptional(dto.Organization);
        }

        if (dto.StartDate.HasValue)
        {
            item.StartDate = dto.StartDate;
        }

        if (dto.EndDate.HasValue)
        {
            item.EndDate = dto.EndDate;
        }

        if (dto.Description != null)
        {
            item.Description = _htmlSanitizer.Sanitize(dto.Description);
        }

        if (dto.StudentEditedBody != null)
        {
            item.StudentEditedBody = _htmlSanitizer.Sanitize(dto.StudentEditedBody);
        }

        if (dto.MediaUrl != null)
        {
            item.MediaUrl = NormalizeOptional(dto.MediaUrl);
        }

        if (dto.ExternalUrl != null)
        {
            item.ExternalUrl = NormalizeOptional(dto.ExternalUrl);
        }

        if (dto.DisplayOrder.HasValue)
        {
            if (dto.DisplayOrder.Value < 0)
            {
                throw ErrorHelper.BadRequest("DisplayOrder cannot be negative.");
            }

            item.DisplayOrder = dto.DisplayOrder.Value;
        }

        if (dto.IsVisible.HasValue)
        {
            item.IsVisible = dto.IsVisible.Value;
        }

        if (dto.AccentColor != null)
        {
            ApplyAccentColor(item, dto.AccentColor);
        }

        if (dto.IsFeatured.HasValue)
        {
            item.IsFeatured = dto.IsFeatured;
        }

        if (dto.Span.HasValue)
        {
            item.Span = dto.Span;
        }

        if (AutoImportedTypes.Contains(item.ItemType))
        {
            item.Source = PortfolioItemSource.StudentEdited;
        }

        if (dto.MediaAssets != null)
        {
            await ReplaceMediaPlacementsAsync(portfolio.Id, item.Id, null, dto.MediaAssets);
        }

        MarkDraftDirty(portfolio);
        await _unitOfWork.PortfolioCustomItems.Update(item);
        await _unitOfWork.Portfolios.Update(portfolio);
        await _unitOfWork.SaveChangesAsync();

        var appendixByItemId = await LoadAppendixByItemIdAsync([item.Id]);
        var mediaByItemId = await LoadItemMediaPlacementsAsync([item.Id]);
        return MapItemResponse(
            item,
            appendixByItemId.GetValueOrDefault(item.Id),
            mediaByItemId.GetValueOrDefault(item.Id));
    }

    public async Task RemoveItemAsync(Guid itemId)
    {
        if (itemId == Guid.Empty)
        {
            throw ErrorHelper.BadRequest("Portfolio item id is required.");
        }

        var student = await GetCurrentStudentAsync();
        var portfolio = await GetRootPortfolioForStudentOrThrowAsync(student.Id);
        var item = await GetOwnedItemOrThrowAsync(portfolio.Id, itemId);

        if (AutoImportedTypes.Contains(item.ItemType))
        {
            throw ErrorHelper.BadRequest("Auto-imported items cannot be deleted. Hide them instead.");
        }

        var placements = await _unitOfWork.PortfolioMediaPlacements.GetAllAsync(
            p => p.PortfolioCustomItemId == itemId && !p.IsDeleted);
        if (placements.Count > 0)
        {
            await _unitOfWork.PortfolioMediaPlacements.SoftRemoveRange(placements);
        }

        await _unitOfWork.PortfolioCustomItems.SoftRemove(item);
        MarkDraftDirty(portfolio);
        await _unitOfWork.Portfolios.Update(portfolio);
        await _unitOfWork.SaveChangesAsync();
    }

    public async Task<PortfolioResponseDto> ReorderItemsAsync(ReorderPortfolioItemsRequestDto dto)
    {
        if (dto == null || dto.Items.Count == 0)
        {
            throw ErrorHelper.BadRequest("At least one item is required to reorder.");
        }

        var student = await GetCurrentStudentAsync();
        var portfolio = await GetRootPortfolioForStudentOrThrowAsync(student.Id);
        var items = await GetPortfolioItemsAsync(portfolio.Id);
        var itemsById = items.ToDictionary(i => i.Id);

        var toUpdate = new List<PortfolioCustomItem>();

        foreach (var entry in dto.Items)
        {
            if (!itemsById.TryGetValue(entry.Id, out var item))
            {
                throw ErrorHelper.NotFound($"Portfolio item with id '{entry.Id}' not found.");
            }

            if (entry.DisplayOrder < 0)
            {
                throw ErrorHelper.BadRequest("DisplayOrder cannot be negative.");
            }

            item.DisplayOrder = entry.DisplayOrder;
            toUpdate.Add(item);
        }

        MarkDraftDirty(portfolio);
        await _unitOfWork.PortfolioCustomItems.UpdateRange(toUpdate);
        await _unitOfWork.Portfolios.Update(portfolio);
        await _unitOfWork.SaveChangesAsync();

        return await MapPortfolioResponseAsync(portfolio);
    }

    public async Task<PortfolioResponseDto> SyncMyPortfolioAsync()
    {
        var student = await GetCurrentStudentAsync();
        var portfolio = await GetRootPortfolioForStudentOrThrowAsync(student.Id);
        var items = await GetPortfolioItemsAsync(portfolio.Id);

        await SyncCertificatesAsync(portfolio, items);
        await SyncCapstoneProjectsAsync(portfolio, items);
        await SyncHighlightReelsAsync(portfolio, items);

        MarkDraftDirty(portfolio);
        await _unitOfWork.Portfolios.Update(portfolio);
        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation(
            "[SyncMyPortfolioAsync] Portfolio {PortfolioId} synced for student {StudentId}.",
            portfolio.Id,
            student.Id);

        return await MapPortfolioResponseAsync(portfolio);
    }

    public async Task<PublicPortfolioResponseDto> GetPublicPortfolioBySubdomainAsync(string subdomain)
    {
        var normalized = PortfolioSubdomainValidator.Normalize(subdomain);
        if (normalized == null || !PortfolioSubdomainValidator.TryValidateFormat(normalized, out _))
        {
            throw ErrorHelper.NotFound("Portfolio not found.");
        }

        var portfolio = await _unitOfWork.Portfolios.FirstOrDefaultAsync(
            p => p.Subdomain == normalized && p.IsPublic && !p.IsDeleted);

        if (portfolio == null)
        {
            throw ErrorHelper.NotFound("Portfolio not found.");
        }

        if (string.IsNullOrWhiteSpace(portfolio.PublishedSnapshot))
        {
            throw ErrorHelper.NotFound("Portfolio not found.");
        }

        var snapshot = JsonSerializer.Deserialize<PublicPortfolioResponseDto>(
            portfolio.PublishedSnapshot,
            JsonOptions);

        if (snapshot == null)
        {
            throw ErrorHelper.NotFound("Portfolio not found.");
        }

        return snapshot;
    }

    public async Task<PortfolioMediaUploadResponseDto> UploadMediaAsync(IFormFile file)
    {
        if (file == null || file.Length == 0)
        {
            throw ErrorHelper.BadRequest("A file is required.");
        }

        var student = await GetCurrentStudentAsync();
        var portfolio = await GetRootPortfolioForStudentOrThrowAsync(student.Id);

        var extension = Path.GetExtension(file.FileName);
        if (!AllowedImageExtensions.TryGetValue(extension, out var expectedContentType))
        {
            throw ErrorHelper.BadRequest("Only .jpg, .jpeg, and .png image files are allowed.");
        }

        if (file.Length > MaxImageBytes)
        {
            throw ErrorHelper.BadRequest("Image file size must not exceed 5 MB.");
        }

        var contentType = string.IsNullOrWhiteSpace(file.ContentType)
            ? expectedContentType
            : file.ContentType.Trim();
        if (!string.Equals(contentType, expectedContentType, StringComparison.OrdinalIgnoreCase))
        {
            throw ErrorHelper.BadRequest($"Content type must be {expectedContentType}.");
        }

        var fileName = $"{Guid.NewGuid():N}{extension.ToLowerInvariant()}";
        var folder = $"portfolio/{student.Id}/{portfolio.Id}";

        await using var stream = file.OpenReadStream();
        await _blobService.UploadFileAsync(fileName, stream, folder);

        var s3Key = $"{folder}/{fileName}";
        var url = await _blobService.GetPreviewUrlAsync(s3Key);

        var asset = new PortfolioMediaAsset
        {
            PortfolioId = portfolio.Id,
            Type = PortfolioMediaType.Image,
            Url = url,
            S3Key = s3Key,
            FileName = Path.GetFileName(file.FileName),
            ContentType = contentType,
            SizeBytes = file.Length,
        };

        await _unitOfWork.PortfolioMediaAssets.AddAsync(asset);
        await _unitOfWork.SaveChangesAsync();

        return new PortfolioMediaUploadResponseDto
        {
            Id = asset.Id,
            Url = asset.Url,
            Type = asset.Type,
            FileName = asset.FileName,
            ContentType = asset.ContentType,
            SizeBytes = asset.SizeBytes,
            CreatedAt = asset.CreatedAt,
        };
    }

    public async Task<List<PortfolioMediaUploadResponseDto>> ListMediaAsync()
    {
        var student = await GetCurrentStudentAsync();
        var portfolio = await GetRootPortfolioForStudentOrThrowAsync(student.Id);

        var assets = await _unitOfWork.PortfolioMediaAssets.GetAllAsync(
            a => a.PortfolioId == portfolio.Id && !a.IsDeleted);

        return assets
            .OrderByDescending(a => a.CreatedAt)
            .Select(a => new PortfolioMediaUploadResponseDto
            {
                Id = a.Id,
                Url = a.Url,
                Type = a.Type,
                FileName = a.FileName,
                ContentType = a.ContentType,
                SizeBytes = a.SizeBytes,
                CreatedAt = a.CreatedAt,
            })
            .ToList();
    }

    public async Task DeleteMediaAsync(Guid mediaId)
    {
        if (mediaId == Guid.Empty)
        {
            throw ErrorHelper.BadRequest("Media id is required.");
        }

        var student = await GetCurrentStudentAsync();
        var portfolio = await GetRootPortfolioForStudentOrThrowAsync(student.Id);
        var asset = await GetOwnedMediaAssetOrThrowAsync(portfolio.Id, mediaId);

        var activePlacements = await _unitOfWork.PortfolioMediaPlacements.GetAllAsync(
            p => p.PortfolioMediaAssetId == asset.Id && !p.IsDeleted);
        if (activePlacements.Count > 0)
        {
            throw ErrorHelper.BadRequest(
                "Remove this media from all portfolio items and sections before deleting it.");
        }

        await _unitOfWork.PortfolioMediaAssets.SoftRemove(asset);
        await _blobService.DeleteByKeyAsync(asset.S3Key);
        await _unitOfWork.SaveChangesAsync();
    }

    public async Task<ImportClassGalleryMediaResponseDto> ImportClassGalleryMediaAsync(
        ImportClassGalleryMediaRequestDto dto)
    {
        if (dto == null || dto.MediaAssetIds == null || dto.MediaAssetIds.Count == 0)
            throw ErrorHelper.BadRequest("At least one media asset id is required.");

        if (dto.PortfolioCustomItemId.HasValue && dto.PortfolioSectionId.HasValue)
            throw ErrorHelper.BadRequest("Provide either PortfolioCustomItemId or PortfolioSectionId, not both.");

        var uniqueIds = new HashSet<Guid>();
        foreach (var id in dto.MediaAssetIds)
        {
            if (id == Guid.Empty)
                throw ErrorHelper.BadRequest("Each media asset id must be non-empty.");
            if (!uniqueIds.Add(id))
                throw ErrorHelper.BadRequest("Duplicate media asset ids are not allowed.");
        }

        var student = await GetCurrentStudentAsync();
        var portfolio = await GetRootPortfolioForStudentOrThrowAsync(student.Id);

        var enrollments = await _unitOfWork.ClassEnrollments.GetAllAsync(
            ce => ce.StudentId == student.Id
                  && ce.Status == ClassEnrollmentStatus.Active
                  && !ce.IsDeleted);
        var enrolledClassIds = enrollments.Select(ce => ce.ClassId).ToHashSet();

        var sourceMedia = await _unitOfWork.MediaAssets.GetAllAsync(
            m => uniqueIds.Contains(m.Id) && !m.IsDeleted);
        var sourceById = sourceMedia.ToDictionary(m => m.Id);

        var existingCopies = await _unitOfWork.PortfolioMediaAssets.GetAllAsync(
            a => a.PortfolioId == portfolio.Id
                 && a.SourceMediaAssetId.HasValue
                 && uniqueIds.Contains(a.SourceMediaAssetId.Value)
                 && !a.IsDeleted);
        var existingBySourceId = existingCopies
            .GroupBy(a => a.SourceMediaAssetId!.Value)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(a => a.CreatedAt).First());

        var imported = new List<PortfolioMediaAsset>();
        var newAssets = new List<PortfolioMediaAsset>();

        foreach (var mediaId in dto.MediaAssetIds)
        {
            if (!sourceById.TryGetValue(mediaId, out var source))
                throw ErrorHelper.NotFound($"Class media asset with id '{mediaId}' not found.");

            if (string.IsNullOrWhiteSpace(source.FileUrl))
                throw ErrorHelper.BadRequest($"Media '{mediaId}' has no file URL and cannot be imported.");

            if (!IsReadyClassMedia(source))
                throw ErrorHelper.BadRequest($"Media '{mediaId}' must be ready before it can be imported.");

            if (!enrolledClassIds.Contains(source.ClassId))
                throw ErrorHelper.Forbidden(
                    $"You must be actively enrolled in the class that owns media '{mediaId}'.");

            if (existingBySourceId.TryGetValue(mediaId, out var existing))
            {
                imported.Add(existing);
                continue;
            }

            var sourceKey = ExtractS3KeyFromFileUrl(source.FileUrl);
            if (string.IsNullOrWhiteSpace(sourceKey))
                throw ErrorHelper.BadRequest($"Could not resolve S3 key for media '{mediaId}'.");

            var extension = Path.GetExtension(sourceKey);
            if (string.IsNullOrWhiteSpace(extension))
                extension = string.Equals(source.FileType, "video", StringComparison.OrdinalIgnoreCase)
                    ? ".mp4"
                    : ".jpg";

            var isVideo = string.Equals(source.FileType, "video", StringComparison.OrdinalIgnoreCase);
            var fileName = $"{Guid.NewGuid():N}{extension.ToLowerInvariant()}";
            var folder = $"portfolio/{student.Id}/{portfolio.Id}";
            var destKey = $"{folder}/{fileName}";

            await _blobService.CopyObjectAsync(sourceKey, destKey);
            var url = await _blobService.GetPreviewUrlAsync(destKey);

            var asset = new PortfolioMediaAsset
            {
                Id = Guid.NewGuid(),
                PortfolioId = portfolio.Id,
                Type = isVideo ? PortfolioMediaType.Video : PortfolioMediaType.Image,
                Url = url,
                S3Key = destKey,
                FileName = Path.GetFileName(sourceKey),
                ContentType = InferContentType(extension, isVideo),
                SizeBytes = 0,
                SourceMediaAssetId = source.Id,
            };

            newAssets.Add(asset);
            imported.Add(asset);
            existingBySourceId[mediaId] = asset;
        }

        if (newAssets.Count > 0)
        {
            await _unitOfWork.PortfolioMediaAssets.AddRangeAsync(newAssets);
            await _unitOfWork.SaveChangesAsync();
        }

        PortfolioCustomItemResponseDto? itemResponse = null;
        PortfolioSectionResponseDto? sectionResponse = null;

        if (dto.PortfolioCustomItemId.HasValue || dto.PortfolioSectionId.HasValue)
        {
            await AppendPlacementsAsync(
                portfolio.Id,
                dto.PortfolioCustomItemId,
                dto.PortfolioSectionId,
                imported);

            MarkDraftDirty(portfolio);
            await _unitOfWork.Portfolios.Update(portfolio);
            await _unitOfWork.SaveChangesAsync();

            if (dto.PortfolioCustomItemId.HasValue)
            {
                var item = await GetOwnedItemOrThrowAsync(portfolio.Id, dto.PortfolioCustomItemId.Value);
                var appendixByItemId = await LoadAppendixByItemIdAsync([item.Id]);
                var mediaByItemId = await LoadItemMediaPlacementsAsync([item.Id]);
                itemResponse = MapItemResponse(
                    item,
                    appendixByItemId.GetValueOrDefault(item.Id),
                    mediaByItemId.GetValueOrDefault(item.Id));
            }
            else
            {
                var section = await GetOwnedSectionOrThrowAsync(portfolio.Id, dto.PortfolioSectionId!.Value);
                var mediaBySectionId = await LoadSectionMediaPlacementsAsync([section.Id]);
                sectionResponse = MapSectionResponse(
                    section,
                    mediaBySectionId.GetValueOrDefault(section.Id));
            }
        }

        return new ImportClassGalleryMediaResponseDto
        {
            Assets = imported.Select(MapUploadResponse).ToList(),
            Item = itemResponse,
            Section = sectionResponse,
        };
    }

    public async Task<PortfolioSectionResponseDto> CreateSectionAsync(CreatePortfolioSectionRequestDto dto)
    {
        if (dto == null)
        {
            throw ErrorHelper.BadRequest("Section data is required.");
        }

        if (!CustomSectionKinds.Contains(dto.Kind))
        {
            throw ErrorHelper.BadRequest("Only custom section kinds can be created manually.");
        }

        var student = await GetCurrentStudentAsync();
        var portfolio = await GetRootPortfolioForStudentOrThrowAsync(student.Id);
        var sections = await GetPortfolioSectionsAsync(portfolio.Id);

        var nextOrder = dto.DisplayOrder ?? (sections.Count == 0 ? 0 : sections.Max(s => s.DisplayOrder) + 1);
        if (nextOrder < 0)
        {
            throw ErrorHelper.BadRequest("DisplayOrder cannot be negative.");
        }

        var section = new PortfolioSection
        {
            PortfolioId = portfolio.Id,
            Kind = dto.Kind,
            Title = NormalizeOptional(dto.Title) ?? GetDefaultSectionTitle(dto.Kind),
            DisplayOrder = nextOrder,
            IsVisible = dto.IsVisible ?? true,
            ContentHtml = _htmlSanitizer.Sanitize(dto.ContentHtml),
            SettingsJson = NormalizeOptional(dto.SettingsJson),
        };

        await _unitOfWork.PortfolioSections.AddAsync(section);
        await _unitOfWork.SaveChangesAsync();

        if (dto.MediaAssets != null)
        {
            await ReplaceMediaPlacementsAsync(portfolio.Id, null, section.Id, dto.MediaAssets);
        }

        MarkDraftDirty(portfolio);
        await _unitOfWork.Portfolios.Update(portfolio);
        await _unitOfWork.SaveChangesAsync();

        var mediaBySectionId = await LoadSectionMediaPlacementsAsync([section.Id]);
        return MapSectionResponse(section, mediaBySectionId.GetValueOrDefault(section.Id));
    }

    public async Task<PortfolioSectionResponseDto> UpdateSectionAsync(
        Guid sectionId,
        UpdatePortfolioSectionRequestDto dto)
    {
        if (sectionId == Guid.Empty)
        {
            throw ErrorHelper.BadRequest("Section id is required.");
        }

        if (dto == null)
        {
            throw ErrorHelper.BadRequest("Section update data is required.");
        }

        var student = await GetCurrentStudentAsync();
        var portfolio = await GetRootPortfolioForStudentOrThrowAsync(student.Id);
        var section = await GetOwnedSectionOrThrowAsync(portfolio.Id, sectionId);
        var isBuiltIn = BuiltInSectionKinds.Contains(section.Kind);

        if (dto.Title != null)
        {
            section.Title = NormalizeOptional(dto.Title);
        }

        if (dto.DisplayOrder.HasValue)
        {
            if (dto.DisplayOrder.Value < 0)
            {
                throw ErrorHelper.BadRequest("DisplayOrder cannot be negative.");
            }

            section.DisplayOrder = dto.DisplayOrder.Value;
        }

        if (dto.IsVisible.HasValue)
        {
            section.IsVisible = dto.IsVisible.Value;
        }

        if (dto.SettingsJson != null)
        {
            section.SettingsJson = NormalizeOptional(dto.SettingsJson);
        }

        if (!isBuiltIn && dto.ContentHtml != null)
        {
            section.ContentHtml = _htmlSanitizer.Sanitize(dto.ContentHtml);
        }

        if (dto.MediaAssets != null)
        {
            await ReplaceMediaPlacementsAsync(portfolio.Id, null, section.Id, dto.MediaAssets);
        }

        MarkDraftDirty(portfolio);
        await _unitOfWork.PortfolioSections.Update(section);
        await _unitOfWork.Portfolios.Update(portfolio);
        await _unitOfWork.SaveChangesAsync();

        var mediaBySectionId = await LoadSectionMediaPlacementsAsync([section.Id]);
        return MapSectionResponse(section, mediaBySectionId.GetValueOrDefault(section.Id));
    }

    public async Task DeleteSectionAsync(Guid sectionId)
    {
        if (sectionId == Guid.Empty)
        {
            throw ErrorHelper.BadRequest("Section id is required.");
        }

        var student = await GetCurrentStudentAsync();
        var portfolio = await GetRootPortfolioForStudentOrThrowAsync(student.Id);
        var section = await GetOwnedSectionOrThrowAsync(portfolio.Id, sectionId);

        if (BuiltInSectionKinds.Contains(section.Kind))
        {
            throw ErrorHelper.BadRequest("Built-in sections cannot be deleted. Hide them instead.");
        }

        var placements = await _unitOfWork.PortfolioMediaPlacements.GetAllAsync(
            p => p.PortfolioSectionId == section.Id && !p.IsDeleted);
        if (placements.Count > 0)
        {
            await _unitOfWork.PortfolioMediaPlacements.SoftRemoveRange(placements);
        }

        await _unitOfWork.PortfolioSections.SoftRemove(section);
        MarkDraftDirty(portfolio);
        await _unitOfWork.Portfolios.Update(portfolio);
        await _unitOfWork.SaveChangesAsync();
    }

    public async Task<PortfolioResponseDto> ReorderSectionsAsync(ReorderPortfolioSectionsRequestDto dto)
    {
        if (dto == null || dto.Sections.Count == 0)
        {
            throw ErrorHelper.BadRequest("At least one section is required to reorder.");
        }

        var student = await GetCurrentStudentAsync();
        var portfolio = await GetRootPortfolioForStudentOrThrowAsync(student.Id);
        var sections = await GetPortfolioSectionsAsync(portfolio.Id);
        var sectionsById = sections.ToDictionary(s => s.Id);

        var toUpdate = new List<PortfolioSection>();

        foreach (var entry in dto.Sections)
        {
            if (!sectionsById.TryGetValue(entry.Id, out var section))
            {
                throw ErrorHelper.NotFound($"Portfolio section with id '{entry.Id}' not found.");
            }

            if (entry.DisplayOrder < 0)
            {
                throw ErrorHelper.BadRequest("DisplayOrder cannot be negative.");
            }

            section.DisplayOrder = entry.DisplayOrder;
            toUpdate.Add(section);
        }

        MarkDraftDirty(portfolio);
        await _unitOfWork.PortfolioSections.UpdateRange(toUpdate);
        await _unitOfWork.Portfolios.Update(portfolio);
        await _unitOfWork.SaveChangesAsync();

        return await MapPortfolioResponseAsync(portfolio);
    }

    public async Task<int> EnsureBuiltInSectionsForAllPortfoliosAsync()
    {
        var portfolios = await _unitOfWork.Portfolios.GetAllAsync(
            p => p.ParentPortfolioId == null && !p.IsDeleted);

        var totalCreated = 0;
        foreach (var portfolio in portfolios)
        {
            totalCreated += await EnsureBuiltInSectionsAsync(portfolio);
        }

        return totalCreated;
    }

    private async Task<int> EnsureBuiltInSectionsAsync(Portfolio portfolio)
    {
        var existingSections = await GetPortfolioSectionsAsync(portfolio.Id);
        var existingKinds = existingSections.Select(s => s.Kind).ToHashSet();
        var theme = DeserializeTheme(portfolio.ThemeConfig);
        var created = 0;

        foreach (var kind in BuiltInSectionKinds)
        {
            if (existingKinds.Contains(kind))
            {
                continue;
            }

            var section = new PortfolioSection
            {
                PortfolioId = portfolio.Id,
                Kind = kind,
                Title = GetBuiltInSectionTitle(kind),
                DisplayOrder = ResolveBuiltInSectionDisplayOrder(kind, theme),
                IsVisible = true,
                ContentHtml = null,
            };

            await _unitOfWork.PortfolioSections.AddAsync(section);
            created++;
        }

        if (created > 0)
        {
            await _unitOfWork.SaveChangesAsync();
        }

        return created;
    }

    private static void MarkDraftDirty(Portfolio portfolio)
    {
        if (portfolio.LastPublishedAt.HasValue)
        {
            portfolio.HasUnpublishedChanges = true;
        }
    }

    private async Task ReplaceMediaPlacementsAsync(
        Guid portfolioId,
        Guid? itemId,
        Guid? sectionId,
        List<PortfolioMediaAssetInputDto> inputs)
    {
        if (itemId.HasValue == sectionId.HasValue)
        {
            throw ErrorHelper.BadRequest("Exactly one media owner (item or section) is required.");
        }

        if (inputs.Count > MaxGalleryAssetsPerOwner)
        {
            throw ErrorHelper.BadRequest(
                $"At most {MaxGalleryAssetsPerOwner} media assets are allowed per gallery.");
        }

        var mediaIds = new HashSet<Guid>();
        foreach (var input in inputs)
        {
            if (!input.Id.HasValue || input.Id.Value == Guid.Empty)
            {
                throw ErrorHelper.BadRequest("Each media asset must include a non-empty id.");
            }

            if (!mediaIds.Add(input.Id.Value))
            {
                throw ErrorHelper.BadRequest("Duplicate media asset ids are not allowed.");
            }

            if (input.DisplayOrder < 0)
            {
                throw ErrorHelper.BadRequest("DisplayOrder cannot be negative.");
            }
        }

        var ownedAssets = mediaIds.Count == 0
            ? new Dictionary<Guid, PortfolioMediaAsset>()
            : (await _unitOfWork.PortfolioMediaAssets.GetAllAsync(
                    a => a.PortfolioId == portfolioId && mediaIds.Contains(a.Id) && !a.IsDeleted))
                .ToDictionary(a => a.Id);

        foreach (var mediaId in mediaIds)
        {
            if (!ownedAssets.ContainsKey(mediaId))
            {
                throw ErrorHelper.NotFound($"Portfolio media asset with id '{mediaId}' not found.");
            }
        }

        var existingPlacements = itemId.HasValue
            ? await _unitOfWork.PortfolioMediaPlacements.GetAllAsync(
                p => p.PortfolioCustomItemId == itemId.Value && !p.IsDeleted)
            : await _unitOfWork.PortfolioMediaPlacements.GetAllAsync(
                p => p.PortfolioSectionId == sectionId!.Value && !p.IsDeleted);

        if (existingPlacements.Count > 0)
        {
            await _unitOfWork.PortfolioMediaPlacements.SoftRemoveRange(existingPlacements);
        }

        if (inputs.Count == 0)
        {
            return;
        }

        var newPlacements = inputs
            .Select(input => new PortfolioMediaPlacement
            {
                PortfolioMediaAssetId = input.Id!.Value,
                PortfolioCustomItemId = itemId,
                PortfolioSectionId = sectionId,
                Caption = NormalizeCaption(input.Caption),
                DisplayOrder = input.DisplayOrder,
            })
            .ToList();

        await _unitOfWork.PortfolioMediaPlacements.AddRangeAsync(newPlacements);
        await _unitOfWork.SaveChangesAsync();
    }

    private async Task SyncCertificatesAsync(Portfolio portfolio, List<PortfolioCustomItem> items)
    {
        var certificates = await _unitOfWork.Certificates.GetAllAsync(
            c => c.StudentId == portfolio.StudentId && !c.IsDeleted);

        if (certificates.Count == 0)
        {
            return;
        }

        var programIds = certificates
            .Where(c => c.ProgramId.HasValue)
            .Select(c => c.ProgramId!.Value)
            .Distinct()
            .ToList();
        var moduleIds = certificates
            .Where(c => c.ModuleId.HasValue)
            .Select(c => c.ModuleId!.Value)
            .Distinct()
            .ToList();

        var programs = programIds.Count == 0
            ? new Dictionary<Guid, Program>()
            : (await _unitOfWork.Programs.GetAllAsync(p => programIds.Contains(p.Id) && !p.IsDeleted))
                .ToDictionary(p => p.Id);
        var modules = moduleIds.Count == 0
            ? new Dictionary<Guid, Module>()
            : (await _unitOfWork.Modules.GetAllAsync(m => moduleIds.Contains(m.Id) && !m.IsDeleted))
                .ToDictionary(m => m.Id);

        var nextOrder = items.Count == 0 ? 0 : items.Max(i => i.DisplayOrder) + 1;

        foreach (var certificate in certificates)
        {
            var existing = items.FirstOrDefault(
                i => i.ItemType == PortfolioItemType.InternalCertificate
                     && i.ReferenceId == certificate.Id
                     && !i.IsDeleted);

            programs.TryGetValue(certificate.ProgramId ?? Guid.Empty, out var program);
            modules.TryGetValue(certificate.ModuleId ?? Guid.Empty, out var module);

            var title = certificate.ModuleId.HasValue
                ? module?.Name ?? "Module Certificate"
                : program?.Name ?? "Program Certificate";

            if (existing == null)
            {
                var item = new PortfolioCustomItem
                {
                    PortfolioId = portfolio.Id,
                    ItemType = PortfolioItemType.InternalCertificate,
                    ReferenceId = certificate.Id,
                    ProgramId = certificate.ProgramId,
                    ModuleId = certificate.ModuleId,
                    Title = title,
                    MediaUrl = certificate.PdfUrl,
                    DisplayOrder = nextOrder++,
                    IsVisible = true,
                    Source = PortfolioItemSource.AutoImported,
                };

                await _unitOfWork.PortfolioCustomItems.AddAsync(item);
                items.Add(item);
                continue;
            }

            if (existing.Source == PortfolioItemSource.StudentEdited)
            {
                continue;
            }

            existing.ProgramId = certificate.ProgramId;
            existing.ModuleId = certificate.ModuleId;
            existing.Title = title;
            existing.MediaUrl = certificate.PdfUrl;
            await _unitOfWork.PortfolioCustomItems.Update(existing);
        }
    }

    private async Task SyncCapstoneProjectsAsync(Portfolio portfolio, List<PortfolioCustomItem> items)
    {
        var submissions = await _unitOfWork.Submissions.GetAllAsync(
            s => s.StudentId == portfolio.StudentId
                 && !s.IsDeleted
                 && s.Status == SubmissionStatus.Graded
                 && s.ResearchMilestoneId != null);

        if (submissions.Count == 0)
        {
            return;
        }

        var milestoneIds = submissions
            .Select(s => s.ResearchMilestoneId!.Value)
            .Distinct()
            .ToList();

        var milestones = (await _unitOfWork.ResearchMilestones.GetAllAsync(
                m => milestoneIds.Contains(m.Id) && m.IsCapstone && !m.IsDeleted))
            .ToDictionary(m => m.Id);

        var capstoneSubmissions = submissions
            .Where(s => milestones.ContainsKey(s.ResearchMilestoneId!.Value))
            .ToList();

        if (capstoneSubmissions.Count == 0)
        {
            return;
        }

        var moduleEnrollmentIds = capstoneSubmissions
            .Where(s => s.ModuleEnrollmentId.HasValue)
            .Select(s => s.ModuleEnrollmentId!.Value)
            .Distinct()
            .ToList();

        var moduleEnrollments = moduleEnrollmentIds.Count == 0
            ? new Dictionary<Guid, ModuleEnrollment>()
            : (await _unitOfWork.ModuleEnrollments.GetAllAsync(
                    me => moduleEnrollmentIds.Contains(me.Id) && !me.IsDeleted))
                .ToDictionary(me => me.Id);

        var moduleIds = moduleEnrollments.Values
            .Select(me => me.ModuleId)
            .Distinct()
            .ToList();

        var modules = moduleIds.Count == 0
            ? new Dictionary<Guid, Module>()
            : (await _unitOfWork.Modules.GetAllAsync(m => moduleIds.Contains(m.Id) && !m.IsDeleted))
                .ToDictionary(m => m.Id);

        var nextOrder = items.Count == 0 ? 0 : items.Max(i => i.DisplayOrder) + 1;

        foreach (var submission in capstoneSubmissions)
        {
            var milestone = milestones[submission.ResearchMilestoneId!.Value];
            moduleEnrollments.TryGetValue(submission.ModuleEnrollmentId ?? Guid.Empty, out var moduleEnrollment);
            modules.TryGetValue(moduleEnrollment?.ModuleId ?? Guid.Empty, out var module);

            var existing = items.FirstOrDefault(
                i => i.ItemType == PortfolioItemType.CapstoneProject
                     && i.SubmissionId == submission.Id
                     && !i.IsDeleted);

            var title = module?.Name ?? milestone.Title;

            if (existing == null)
            {
                var item = new PortfolioCustomItem
                {
                    PortfolioId = portfolio.Id,
                    ItemType = PortfolioItemType.CapstoneProject,
                    SubmissionId = submission.Id,
                    ModuleEnrollmentId = submission.ModuleEnrollmentId,
                    ModuleId = moduleEnrollment?.ModuleId,
                    ProgramEnrollmentId = moduleEnrollment?.ProgramEnrollmentId,
                    ProgramId = module?.ProgramId,
                    Title = title,
                    Description = submission.ContentText,
                    MentorEndorsement = submission.MentorFeedback,
                    MediaUrl = submission.FileUrl,
                    DisplayOrder = nextOrder++,
                    IsVisible = true,
                    Source = PortfolioItemSource.AutoImported,
                };

                await _unitOfWork.PortfolioCustomItems.AddAsync(item);
                items.Add(item);
                await SyncCapstoneAppendixAsync(item, submission, portfolio.StudentId);
                continue;
            }

            if (existing.Source != PortfolioItemSource.StudentEdited)
            {
                existing.ModuleEnrollmentId = submission.ModuleEnrollmentId;
                existing.ModuleId = moduleEnrollment?.ModuleId;
                existing.ProgramEnrollmentId = moduleEnrollment?.ProgramEnrollmentId;
                existing.ProgramId = module?.ProgramId;
                existing.Title = title;
                existing.Description = submission.ContentText;
                existing.MentorEndorsement = submission.MentorFeedback;
                existing.MediaUrl = submission.FileUrl;
                await _unitOfWork.PortfolioCustomItems.Update(existing);
            }

            await SyncCapstoneAppendixAsync(existing, submission, portfolio.StudentId);
        }
    }

    /// <summary>
    /// Imports completed class-scoped highlight videos as <see cref="PortfolioItemType.HighlightReel"/>
    /// items. Keyed by stack id so regenerations/trims refresh the same portfolio row.
    /// </summary>
    private async Task SyncHighlightReelsAsync(Portfolio portfolio, List<PortfolioCustomItem> items)
    {
        var stacks = await _unitOfWork.HighlightVideoStacks.GetAllAsync(
            s => s.StudentId == portfolio.StudentId && !s.IsDeleted);

        if (stacks.Count == 0)
            return;

        var stackIds = stacks.Select(s => s.Id).ToList();
        var stackItems = await _unitOfWork.HighlightVideoItems.GetAllAsync(
            i => stackIds.Contains(i.StackId) && !i.IsDeleted);

        var latestCompletedByStack = stackItems
            .Where(i => i.Status == HighlightVideoStatus.Completed
                        && !string.IsNullOrWhiteSpace(i.VideoUrl))
            .GroupBy(i => i.StackId)
            .ToDictionary(
                g => g.Key,
                g => g.OrderByDescending(i => i.CreatedAt).First());

        if (latestCompletedByStack.Count == 0)
            return;

        var classIds = stacks.Select(s => s.ClassId).Distinct().ToList();
        var classes = (await _unitOfWork.Classes.GetAllAsync(
                c => classIds.Contains(c.Id) && !c.IsDeleted))
            .ToDictionary(c => c.Id);

        var programIds = classes.Values.Select(c => c.ProgramId).Distinct().ToList();
        var programs = programIds.Count == 0
            ? new Dictionary<Guid, Program>()
            : (await _unitOfWork.Programs.GetAllAsync(p => programIds.Contains(p.Id) && !p.IsDeleted))
                .ToDictionary(p => p.Id);

        var nextOrder = items.Count == 0 ? 0 : items.Max(i => i.DisplayOrder) + 1;

        foreach (var stack in stacks)
        {
            if (!latestCompletedByStack.TryGetValue(stack.Id, out var video))
                continue;

            classes.TryGetValue(stack.ClassId, out var classEntity);
            programs.TryGetValue(classEntity?.ProgramId ?? Guid.Empty, out var program);

            var title = string.IsNullOrWhiteSpace(stack.StrengthDescription)
                ? $"{program?.Name ?? classEntity?.Name ?? "Class"} Highlights"
                : stack.StrengthDescription.Trim();

            var existing = items.FirstOrDefault(
                i => i.ItemType == PortfolioItemType.HighlightReel
                     && i.ReferenceId == stack.Id
                     && !i.IsDeleted);

            if (existing == null)
            {
                var item = new PortfolioCustomItem
                {
                    PortfolioId = portfolio.Id,
                    ItemType = PortfolioItemType.HighlightReel,
                    ReferenceId = stack.Id,
                    ProgramId = classEntity?.ProgramId,
                    Title = title,
                    MediaUrl = video.VideoUrl,
                    DisplayOrder = nextOrder++,
                    IsVisible = true,
                    Source = PortfolioItemSource.AutoImported,
                };

                await _unitOfWork.PortfolioCustomItems.AddAsync(item);
                items.Add(item);
                continue;
            }

            if (existing.Source == PortfolioItemSource.StudentEdited)
                continue;

            existing.ProgramId = classEntity?.ProgramId;
            existing.Title = title;
            existing.MediaUrl = video.VideoUrl;
            await _unitOfWork.PortfolioCustomItems.Update(existing);
        }
    }

    private async Task SyncCapstoneAppendixAsync(
        PortfolioCustomItem capstoneItem,
        Submission capstoneSubmission,
        Guid studentId)
    {
        if (!capstoneSubmission.ModuleEnrollmentId.HasValue)
        {
            return;
        }

        var moduleEnrollmentId = capstoneSubmission.ModuleEnrollmentId.Value;
        var priorSubmissions = await _unitOfWork.Submissions.GetAllAsync(
            s => s.StudentId == studentId
                 && !s.IsDeleted
                 && s.ModuleEnrollmentId == moduleEnrollmentId
                 && s.Status == SubmissionStatus.Graded
                 && s.ResearchMilestoneId != null
                 && s.Id != capstoneSubmission.Id);

        if (priorSubmissions.Count == 0)
        {
            return;
        }

        var milestoneIds = priorSubmissions
            .Select(s => s.ResearchMilestoneId!.Value)
            .Distinct()
            .ToList();

        var milestones = (await _unitOfWork.ResearchMilestones.GetAllAsync(
                m => milestoneIds.Contains(m.Id) && !m.IsCapstone && !m.IsDeleted))
            .ToDictionary(m => m.Id);

        var existingAppendix = await _unitOfWork.PortfolioItemSubmissions.GetAllAsync(
            a => a.PortfolioCustomItemId == capstoneItem.Id && !a.IsDeleted);

        var order = existingAppendix.Count == 0 ? 0 : existingAppendix.Max(a => a.DisplayOrder) + 1;

        foreach (var priorSubmission in priorSubmissions)
        {
            if (!milestones.TryGetValue(priorSubmission.ResearchMilestoneId!.Value, out var milestone))
            {
                continue;
            }

            if (existingAppendix.Any(a => a.SubmissionId == priorSubmission.Id))
            {
                continue;
            }

            var appendix = new PortfolioItemSubmission
            {
                PortfolioCustomItemId = capstoneItem.Id,
                SubmissionId = priorSubmission.Id,
                SectionTitle = milestone.Title,
                DisplayOrder = order++,
            };

            await _unitOfWork.PortfolioItemSubmissions.AddAsync(appendix);
            existingAppendix.Add(appendix);
        }
    }

    private async Task ApplySubdomainAsync(Portfolio portfolio, string? subdomainInput)
    {
        var normalized = PortfolioSubdomainValidator.Normalize(subdomainInput);
        if (normalized == null)
        {
            if (portfolio.IsPublic)
            {
                throw ErrorHelper.BadRequest("A subdomain is required while the portfolio is public.");
            }

            portfolio.Subdomain = null;
            return;
        }

        if (!PortfolioSubdomainValidator.TryValidateFormat(normalized, out var reason))
        {
            throw ErrorHelper.BadRequest(reason ?? "Invalid subdomain.");
        }

        var availability = await BuildSubdomainAvailabilityAsync(normalized, portfolio.Id);
        if (!availability.Available)
        {
            throw ErrorHelper.Conflict(availability.Reason ?? "Subdomain is already taken.");
        }

        portfolio.Subdomain = normalized;
    }

    private static void EnsureCanPublish(Portfolio portfolio)
    {
        if (string.IsNullOrWhiteSpace(portfolio.Subdomain))
        {
            throw ErrorHelper.BadRequest(
                "Set and verify a unique subdomain before publishing your portfolio.");
        }

        if (!PortfolioSubdomainValidator.TryValidateFormat(portfolio.Subdomain, out var reason))
        {
            throw ErrorHelper.BadRequest(reason ?? "Invalid subdomain.");
        }
    }

    private static void ApplyTheme(Portfolio portfolio, ThemeConfigDto theme)
    {
        portfolio.ThemeConfig = JsonSerializer.Serialize(theme, JsonOptions);
        portfolio.TemplateId = theme.TemplateId;
        portfolio.PrimaryColor = theme.PrimaryColor;
    }

    private async Task<SubdomainAvailabilityResponseDto> BuildSubdomainAvailabilityAsync(
        string subdomainInput,
        Guid excludePortfolioId)
    {
        var normalized = PortfolioSubdomainValidator.Normalize(subdomainInput);
        if (normalized == null)
        {
            return new SubdomainAvailabilityResponseDto
            {
                Subdomain = string.Empty,
                Available = false,
                Reason = "Subdomain is required.",
            };
        }

        if (!PortfolioSubdomainValidator.TryValidateFormat(normalized, out var reason))
        {
            return new SubdomainAvailabilityResponseDto
            {
                Subdomain = normalized,
                Available = false,
                Reason = reason,
            };
        }

        var taken = await _unitOfWork.Portfolios.FirstOrDefaultAsync(
            p => p.Subdomain == normalized && p.Id != excludePortfolioId && !p.IsDeleted);

        return new SubdomainAvailabilityResponseDto
        {
            Subdomain = normalized,
            Available = taken == null,
            Reason = taken == null ? null : "This subdomain is already taken.",
        };
    }

    private async Task<User> GetCurrentStudentAsync()
    {
        var userId = _claimsService.GetCurrentUserId;
        if (userId == Guid.Empty)
        {
            throw ErrorHelper.Unauthorized("Unauthorized access.");
        }

        var user = await _unitOfWork.Users.GetByIdAsync(userId);
        if (user == null || user.IsDeleted)
        {
            throw ErrorHelper.NotFound("Current user not found.");
        }

        if (user.Role != RoleType.Student)
        {
            throw ErrorHelper.Forbidden("Only students can manage portfolios.");
        }

        return user;
    }

    private async Task<Portfolio> GetRootPortfolioForStudentOrThrowAsync(Guid studentId)
    {
        var portfolio = await _unitOfWork.Portfolios.FirstOrDefaultAsync(
            p => p.StudentId == studentId && p.ParentPortfolioId == null && !p.IsDeleted,
            p => p.Student);

        if (portfolio == null)
        {
            throw ErrorHelper.NotFound("Portfolio not found.");
        }

        return portfolio;
    }

    private async Task<PortfolioCustomItem> GetOwnedItemOrThrowAsync(Guid portfolioId, Guid itemId)
    {
        var item = await _unitOfWork.PortfolioCustomItems.FirstOrDefaultAsync(
            i => i.Id == itemId && i.PortfolioId == portfolioId && !i.IsDeleted,
            i => i.Program!,
            i => i.Module!);

        if (item == null)
        {
            throw ErrorHelper.NotFound($"Portfolio item with id '{itemId}' not found.");
        }

        return item;
    }

    private async Task<PortfolioSection> GetOwnedSectionOrThrowAsync(Guid portfolioId, Guid sectionId)
    {
        var section = await _unitOfWork.PortfolioSections.FirstOrDefaultAsync(
            s => s.Id == sectionId && s.PortfolioId == portfolioId && !s.IsDeleted);

        if (section == null)
        {
            throw ErrorHelper.NotFound($"Portfolio section with id '{sectionId}' not found.");
        }

        return section;
    }

    private async Task<PortfolioMediaAsset> GetOwnedMediaAssetOrThrowAsync(Guid portfolioId, Guid mediaId)
    {
        var asset = await _unitOfWork.PortfolioMediaAssets.FirstOrDefaultAsync(
            a => a.Id == mediaId && a.PortfolioId == portfolioId && !a.IsDeleted);

        if (asset == null)
        {
            throw ErrorHelper.NotFound($"Portfolio media asset with id '{mediaId}' not found.");
        }

        return asset;
    }

    private async Task<List<PortfolioCustomItem>> GetPortfolioItemsAsync(Guid portfolioId)
    {
        return (await _unitOfWork.PortfolioCustomItems.GetAllAsync(
                i => i.PortfolioId == portfolioId && !i.IsDeleted,
                i => i.Program!,
                i => i.Module!))
            .OrderBy(i => i.DisplayOrder)
            .ThenBy(i => i.CreatedAt)
            .ToList();
    }

    private async Task<List<PortfolioSection>> GetPortfolioSectionsAsync(Guid portfolioId)
    {
        return (await _unitOfWork.PortfolioSections.GetAllAsync(
                s => s.PortfolioId == portfolioId && !s.IsDeleted))
            .OrderBy(s => s.DisplayOrder)
            .ThenBy(s => s.CreatedAt)
            .ToList();
    }

    private async Task<Dictionary<Guid, List<PortfolioMediaPlacement>>> LoadItemMediaPlacementsAsync(
        List<Guid> itemIds)
    {
        if (itemIds.Count == 0)
        {
            return new Dictionary<Guid, List<PortfolioMediaPlacement>>();
        }

        var placements = await _unitOfWork.PortfolioMediaPlacements.GetAllAsync(
            p => p.PortfolioCustomItemId.HasValue
                 && itemIds.Contains(p.PortfolioCustomItemId.Value)
                 && !p.IsDeleted,
            p => p.MediaAsset);

        return placements
            .GroupBy(p => p.PortfolioCustomItemId!.Value)
            .ToDictionary(
                g => g.Key,
                g => g.OrderBy(p => p.DisplayOrder).ThenBy(p => p.CreatedAt).ToList());
    }

    private async Task<Dictionary<Guid, List<PortfolioMediaPlacement>>> LoadSectionMediaPlacementsAsync(
        List<Guid> sectionIds)
    {
        if (sectionIds.Count == 0)
        {
            return new Dictionary<Guid, List<PortfolioMediaPlacement>>();
        }

        var placements = await _unitOfWork.PortfolioMediaPlacements.GetAllAsync(
            p => p.PortfolioSectionId.HasValue
                 && sectionIds.Contains(p.PortfolioSectionId.Value)
                 && !p.IsDeleted,
            p => p.MediaAsset);

        return placements
            .GroupBy(p => p.PortfolioSectionId!.Value)
            .ToDictionary(
                g => g.Key,
                g => g.OrderBy(p => p.DisplayOrder).ThenBy(p => p.CreatedAt).ToList());
    }

    private async Task<Dictionary<Guid, List<PortfolioAppendixItemDto>>> LoadAppendixByItemIdAsync(
        List<Guid> itemIds)
    {
        if (itemIds.Count == 0)
        {
            return new Dictionary<Guid, List<PortfolioAppendixItemDto>>();
        }

        var appendixRows = await _unitOfWork.PortfolioItemSubmissions.GetAllAsync(
            a => itemIds.Contains(a.PortfolioCustomItemId) && !a.IsDeleted,
            a => a.Submission);

        var milestoneIds = appendixRows
            .Where(a => a.Submission.ResearchMilestoneId.HasValue)
            .Select(a => a.Submission.ResearchMilestoneId!.Value)
            .Distinct()
            .ToList();

        var milestones = milestoneIds.Count == 0
            ? new Dictionary<Guid, ResearchMilestone>()
            : (await _unitOfWork.ResearchMilestones.GetAllAsync(
                    m => milestoneIds.Contains(m.Id) && !m.IsDeleted))
                .ToDictionary(m => m.Id);

        return appendixRows
            .GroupBy(a => a.PortfolioCustomItemId)
            .ToDictionary(
                g => g.Key,
                g => g.OrderBy(a => a.DisplayOrder)
                    .Select(a =>
                    {
                        milestones.TryGetValue(
                            a.Submission.ResearchMilestoneId ?? Guid.Empty,
                            out var milestone);

                        return new PortfolioAppendixItemDto
                        {
                            Id = a.Id,
                            SubmissionId = a.SubmissionId,
                            SectionTitle = a.SectionTitle,
                            DisplayOrder = a.DisplayOrder,
                            ContentText = a.Submission.ContentText,
                            FileUrl = a.Submission.FileUrl,
                            AssignedGrade = a.Submission.AssignedGrade,
                            MilestoneTitle = milestone?.Title,
                        };
                    })
                    .ToList());
    }

    private async Task<PortfolioResponseDto> MapPortfolioResponseAsync(Portfolio portfolio)
    {
        var student = portfolio.Student
            ?? await _unitOfWork.Users.GetByIdAsync(portfolio.StudentId);

        var items = await GetPortfolioItemsAsync(portfolio.Id);
        var sections = await GetPortfolioSectionsAsync(portfolio.Id);
        var appendixByItemId = await LoadAppendixByItemIdAsync(items.Select(i => i.Id).ToList());
        var itemMediaById = await LoadItemMediaPlacementsAsync(items.Select(i => i.Id).ToList());
        var sectionMediaById = await LoadSectionMediaPlacementsAsync(sections.Select(s => s.Id).ToList());

        return new PortfolioResponseDto
        {
            Id = portfolio.Id,
            Code = portfolio.Code,
            StudentId = portfolio.StudentId,
            StudentName = student?.FullName,
            AvatarUrl = portfolio.AvatarUrl ?? student?.AvatarUrl,
            CoverImageUrl = portfolio.CoverImageUrl,
            Subdomain = portfolio.Subdomain,
            DisplayName = portfolio.DisplayName,
            Headline = portfolio.Headline,
            Tagline = portfolio.Tagline,
            Summary = portfolio.Summary,
            PlanType = portfolio.PlanType,
            IsPublic = portfolio.IsPublic,
            LastPublishedAt = portfolio.LastPublishedAt,
            HasUnpublishedChanges = portfolio.HasUnpublishedChanges,
            Theme = DeserializeTheme(portfolio.ThemeConfig),
            Links = DeserializeLinks(portfolio.Links),
            Items = items
                .Select(i => MapItemResponse(
                    i,
                    appendixByItemId.GetValueOrDefault(i.Id),
                    itemMediaById.GetValueOrDefault(i.Id)))
                .ToList(),
            Sections = sections
                .Select(s => MapSectionResponse(s, sectionMediaById.GetValueOrDefault(s.Id)))
                .ToList(),
            CreatedAt = portfolio.CreatedAt,
            UpdatedAt = portfolio.UpdatedAt,
        };
    }

    private async Task<PublicPortfolioResponseDto> BuildPublicSnapshotAsync(Portfolio portfolio)
    {
        var student = portfolio.Student
            ?? await _unitOfWork.Users.GetByIdAsync(portfolio.StudentId);

        var items = (await GetPortfolioItemsAsync(portfolio.Id))
            .Where(i => i.IsVisible)
            .ToList();
        var sections = (await GetPortfolioSectionsAsync(portfolio.Id))
            .Where(s => s.IsVisible)
            .ToList();

        var appendixByItemId = await LoadAppendixByItemIdAsync(items.Select(i => i.Id).ToList());
        var itemMediaById = await LoadItemMediaPlacementsAsync(items.Select(i => i.Id).ToList());
        var sectionMediaById = await LoadSectionMediaPlacementsAsync(sections.Select(s => s.Id).ToList());

        return new PublicPortfolioResponseDto
        {
            Subdomain = portfolio.Subdomain,
            DisplayName = portfolio.DisplayName,
            Headline = portfolio.Headline,
            Tagline = portfolio.Tagline,
            Summary = portfolio.Summary,
            StudentName = student?.FullName,
            AvatarUrl = portfolio.AvatarUrl ?? student?.AvatarUrl,
            CoverImageUrl = portfolio.CoverImageUrl,
            Theme = DeserializeTheme(portfolio.ThemeConfig),
            Links = DeserializeLinks(portfolio.Links),
            Items = items
                .Select(i => MapItemResponse(
                    i,
                    appendixByItemId.GetValueOrDefault(i.Id),
                    itemMediaById.GetValueOrDefault(i.Id)))
                .ToList(),
            Sections = sections
                .Select(s => MapSectionResponse(s, sectionMediaById.GetValueOrDefault(s.Id)))
                .ToList(),
        };
    }

    private static PortfolioCustomItemResponseDto MapItemResponse(
        PortfolioCustomItem item,
        List<PortfolioAppendixItemDto>? appendixSections = null,
        List<PortfolioMediaPlacement>? mediaPlacements = null)
    {
        return new PortfolioCustomItemResponseDto
        {
            Id = item.Id,
            ItemType = item.ItemType,
            Title = item.Title,
            Subtitle = item.Subtitle,
            Organization = item.Organization,
            StartDate = item.StartDate,
            EndDate = item.EndDate,
            Description = item.Description,
            MentorEndorsement = item.MentorEndorsement,
            StudentEditedBody = item.StudentEditedBody,
            MediaUrl = item.MediaUrl,
            ExternalUrl = item.ExternalUrl,
            DisplayOrder = item.DisplayOrder,
            IsVisible = item.IsVisible,
            Source = item.Source,
            AccentColor = item.AccentColor,
            IsFeatured = item.IsFeatured,
            Span = item.Span,
            MediaAssets = (mediaPlacements ?? [])
                .OrderBy(p => p.DisplayOrder)
                .ThenBy(p => p.CreatedAt)
                .Select(MapMediaPlacement)
                .ToList(),
            ProgramId = item.ProgramId,
            ProgramName = item.Program?.Name,
            ModuleId = item.ModuleId,
            ModuleName = item.Module?.Name,
            ModuleEnrollmentId = item.ModuleEnrollmentId,
            SubmissionId = item.SubmissionId,
            AppendixSections = appendixSections ?? [],
            CreatedAt = item.CreatedAt,
            UpdatedAt = item.UpdatedAt,
        };
    }

    private static PortfolioSectionResponseDto MapSectionResponse(
        PortfolioSection section,
        List<PortfolioMediaPlacement>? mediaPlacements = null)
    {
        return new PortfolioSectionResponseDto
        {
            Id = section.Id,
            Kind = section.Kind,
            Title = section.Title,
            DisplayOrder = section.DisplayOrder,
            IsVisible = section.IsVisible,
            ContentHtml = section.ContentHtml,
            SettingsJson = section.SettingsJson,
            MediaAssets = (mediaPlacements ?? [])
                .OrderBy(p => p.DisplayOrder)
                .ThenBy(p => p.CreatedAt)
                .Select(MapMediaPlacement)
                .ToList(),
            CreatedAt = section.CreatedAt,
            UpdatedAt = section.UpdatedAt,
        };
    }

    private static PortfolioMediaAssetResponseDto MapMediaPlacement(PortfolioMediaPlacement placement)
    {
        var asset = placement.MediaAsset;
        return new PortfolioMediaAssetResponseDto
        {
            Id = asset.Id,
            Url = asset.Url,
            Type = asset.Type,
            Caption = placement.Caption,
            DisplayOrder = placement.DisplayOrder,
        };
    }

    private static ThemeConfigDto? DeserializeTheme(string? themeConfig)
    {
        if (string.IsNullOrWhiteSpace(themeConfig))
        {
            return null;
        }

        return JsonSerializer.Deserialize<ThemeConfigDto>(themeConfig, JsonOptions);
    }

    private static List<PortfolioLinkDto> DeserializeLinks(string? linksJson)
    {
        if (string.IsNullOrWhiteSpace(linksJson))
        {
            return [];
        }

        return JsonSerializer.Deserialize<List<PortfolioLinkDto>>(linksJson, JsonOptions) ?? [];
    }

    private static string? NormalizeOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static string? NormalizeCaption(string? caption)
    {
        if (string.IsNullOrWhiteSpace(caption))
        {
            return null;
        }

        var trimmed = caption.Trim();
        if (trimmed.Length > 255)
        {
            throw ErrorHelper.BadRequest("Caption must be at most 255 characters.");
        }

        return trimmed;
    }

    private async Task AppendPlacementsAsync(
        Guid portfolioId,
        Guid? itemId,
        Guid? sectionId,
        List<PortfolioMediaAsset> assets)
    {
        if (itemId.HasValue == sectionId.HasValue)
            throw ErrorHelper.BadRequest("Exactly one media owner (item or section) is required.");

        if (itemId.HasValue)
            await GetOwnedItemOrThrowAsync(portfolioId, itemId.Value);
        else
            await GetOwnedSectionOrThrowAsync(portfolioId, sectionId!.Value);

        var existingPlacements = itemId.HasValue
            ? await _unitOfWork.PortfolioMediaPlacements.GetAllAsync(
                p => p.PortfolioCustomItemId == itemId.Value && !p.IsDeleted)
            : await _unitOfWork.PortfolioMediaPlacements.GetAllAsync(
                p => p.PortfolioSectionId == sectionId!.Value && !p.IsDeleted);

        var alreadyPlaced = existingPlacements.Select(p => p.PortfolioMediaAssetId).ToHashSet();
        var toAdd = assets.Where(a => !alreadyPlaced.Contains(a.Id)).ToList();

        if (existingPlacements.Count + toAdd.Count > MaxGalleryAssetsPerOwner)
        {
            throw ErrorHelper.BadRequest(
                $"At most {MaxGalleryAssetsPerOwner} media assets are allowed per gallery.");
        }

        if (toAdd.Count == 0)
            return;

        var nextOrder = existingPlacements.Count == 0
            ? 0
            : existingPlacements.Max(p => p.DisplayOrder) + 1;

        var newPlacements = toAdd
            .Select((asset, index) => new PortfolioMediaPlacement
            {
                PortfolioMediaAssetId = asset.Id,
                PortfolioCustomItemId = itemId,
                PortfolioSectionId = sectionId,
                DisplayOrder = nextOrder + index,
            })
            .ToList();

        await _unitOfWork.PortfolioMediaPlacements.AddRangeAsync(newPlacements);
        await _unitOfWork.SaveChangesAsync();
    }

    private static bool IsReadyClassMedia(MediaAsset media) =>
        !string.Equals(media.FileType, "video", StringComparison.OrdinalIgnoreCase)
        || media.VideoStatus == VideoProcessingStatus.TaggingComplete;

    private string? ExtractS3KeyFromFileUrl(string? fileUrl)
    {
        if (string.IsNullOrWhiteSpace(fileUrl))
            return null;

        try
        {
            var uri = new Uri(fileUrl);
            var s3Key = uri.AbsolutePath.TrimStart('/');
            var bucketPrefix = $"{_blobService.BucketName}/";
            if (s3Key.StartsWith(bucketPrefix, StringComparison.OrdinalIgnoreCase))
                s3Key = s3Key[bucketPrefix.Length..];
            return s3Key;
        }
        catch (UriFormatException)
        {
            return fileUrl.Trim();
        }
    }

    private static string InferContentType(string extension, bool isVideo)
    {
        return extension.ToLowerInvariant() switch
        {
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".mp4" => "video/mp4",
            ".mov" => "video/quicktime",
            _ => isVideo ? "video/mp4" : "image/jpeg",
        };
    }

    private static PortfolioMediaUploadResponseDto MapUploadResponse(PortfolioMediaAsset asset) =>
        new()
        {
            Id = asset.Id,
            Url = asset.Url,
            Type = asset.Type,
            FileName = asset.FileName,
            ContentType = asset.ContentType,
            SizeBytes = asset.SizeBytes,
            CreatedAt = asset.CreatedAt,
        };

    private static string ValidateAndTrimOptionalUrl(string value, string fieldName)
    {
        PortfolioThemeValidator.ValidateOptionalUrl(value, fieldName, 500);
        return value.Trim();
    }

    private static void ApplyAccentColor(PortfolioCustomItem item, string? accentColor)
    {
        if (string.IsNullOrWhiteSpace(accentColor))
        {
            item.AccentColor = null;
            return;
        }

        PortfolioThemeValidator.ValidateHexColor(accentColor, nameof(accentColor));
        item.AccentColor = accentColor.Trim();
    }

    private static string GetBuiltInSectionTitle(PortfolioSectionKind kind)
    {
        return kind switch
        {
            PortfolioSectionKind.ProjectsGroup => "Projects",
            PortfolioSectionKind.ActivitiesGroup => "Activities",
            PortfolioSectionKind.LinksGroup => "Links",
            _ => kind.ToString(),
        };
    }

    private static string GetDefaultSectionTitle(PortfolioSectionKind kind)
    {
        return kind switch
        {
            PortfolioSectionKind.RichText => "Rich Text",
            PortfolioSectionKind.Gallery => "Gallery",
            PortfolioSectionKind.Embed => "Embed",
            _ => kind.ToString(),
        };
    }

    private static int ResolveBuiltInSectionDisplayOrder(PortfolioSectionKind kind, ThemeConfigDto? theme)
    {
        const int defaultProjects = 0;
        const int defaultActivities = 1;
        const int defaultLinks = 2;

        var defaultOrder = kind switch
        {
            PortfolioSectionKind.ProjectsGroup => defaultProjects,
            PortfolioSectionKind.ActivitiesGroup => defaultActivities,
            PortfolioSectionKind.LinksGroup => defaultLinks,
            _ => 0,
        };

        if (theme?.SectionOrder == null || theme.SectionOrder.Count == 0)
        {
            return defaultOrder;
        }

        for (var index = 0; index < theme.SectionOrder.Count; index++)
        {
            var token = theme.SectionOrder[index]?.Trim().ToLowerInvariant();
            if (string.IsNullOrEmpty(token))
            {
                continue;
            }

            if (TokenMatchesBuiltInKind(token, kind))
            {
                return index;
            }
        }

        return defaultOrder;
    }

    private static bool TokenMatchesBuiltInKind(string token, PortfolioSectionKind kind)
    {
        return kind switch
        {
            PortfolioSectionKind.ProjectsGroup => token == "projects",
            PortfolioSectionKind.ActivitiesGroup => token is "certificates"
                or "activities"
                or "experience"
                or "skills",
            PortfolioSectionKind.LinksGroup => token == "links",
            _ => false,
        };
    }

    private async Task<string> GenerateUniquePortfolioCodeAsync()
    {
        for (var attempt = 0; attempt < 8; attempt++)
        {
            var suffix = Convert.ToHexString(RandomNumberGenerator.GetBytes(5));
            var code = $"OBOX-PF-{suffix}";
            var collision = await _unitOfWork.Portfolios.FirstOrDefaultAsync(p => p.Code == code);
            if (collision == null)
            {
                return code;
            }
        }

        throw ErrorHelper.Internal("Failed to generate a unique portfolio code.");
    }
}
