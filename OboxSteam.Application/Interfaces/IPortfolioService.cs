using Microsoft.AspNetCore.Http;
using OboxSteam.Application.DTOs.PortfolioDTO;

namespace OboxSteam.Application.Interfaces;

public interface IPortfolioService
{
    Task<PortfolioResponseDto> GetMyPortfolioAsync();

    Task<PortfolioResponseDto> CreateMyPortfolioAsync();

    Task<PortfolioResponseDto> UpdateMyPortfolioAsync(UpdatePortfolioRequestDto dto);

    Task<SubdomainAvailabilityResponseDto> CheckSubdomainAvailabilityAsync(string subdomain);

    Task<PortfolioResponseDto> UpdateMySubdomainAsync(UpdatePortfolioSubdomainRequestDto dto);

    Task<PortfolioResponseDto> UpdateMyPublicationAsync(UpdatePortfolioPublicationRequestDto dto);

    Task<PortfolioCustomItemResponseDto> AddItemAsync(CreatePortfolioItemRequestDto dto);

    Task<PortfolioCustomItemResponseDto> UpdateItemAsync(Guid itemId, UpdatePortfolioItemRequestDto dto);

    Task RemoveItemAsync(Guid itemId);

    Task<PortfolioResponseDto> ReorderItemsAsync(ReorderPortfolioItemsRequestDto dto);

    Task<PortfolioResponseDto> SyncMyPortfolioAsync();

    Task<PublicPortfolioResponseDto> GetPublicPortfolioBySubdomainAsync(string subdomain);

    Task<PortfolioMediaUploadResponseDto> UploadMediaAsync(IFormFile file);

    Task<List<PortfolioMediaUploadResponseDto>> ListMediaAsync();

    Task DeleteMediaAsync(Guid mediaId);

    /// <summary>
    /// Copies ready class-gallery media into portfolio-owned assets (independent S3 objects).
    /// Optionally appends placements to an item or section gallery.
    /// </summary>
    Task<ImportClassGalleryMediaResponseDto> ImportClassGalleryMediaAsync(
        ImportClassGalleryMediaRequestDto dto);

    /// <summary>
    /// Copies a completed highlight reel into a portfolio-owned Video asset and appends it
    /// to a Gallery section. Idempotent by (portfolioId, sourceHighlightVideoItemId).
    /// Does not create HighlightReel portfolio items.
    /// </summary>
    Task<ImportClassGalleryMediaResponseDto> ImportHighlightReelMediaAsync(
        ImportHighlightReelMediaRequestDto dto);

    Task<PortfolioSectionResponseDto> CreateSectionAsync(CreatePortfolioSectionRequestDto dto);

    Task<PortfolioSectionResponseDto> UpdateSectionAsync(Guid sectionId, UpdatePortfolioSectionRequestDto dto);

    Task DeleteSectionAsync(Guid sectionId);

    Task<PortfolioResponseDto> ReorderSectionsAsync(ReorderPortfolioSectionsRequestDto dto);

    /// <summary>Idempotent backfill of built-in group sections for all root portfolios.</summary>
    Task<int> EnsureBuiltInSectionsForAllPortfoliosAsync();
}
