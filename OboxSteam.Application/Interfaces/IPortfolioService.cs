using OboxSteam.Application.DTOs.PortfolioDTO;

namespace OboxSteam.Application.Interfaces;

public interface IPortfolioService
{
    Task<PortfolioResponseDto> GetMyPortfolioAsync();

    Task<PortfolioResponseDto> CreateMyPortfolioAsync();

    Task<PortfolioResponseDto> UpdateMyPortfolioAsync(UpdatePortfolioRequestDto dto);

    Task<SubdomainAvailabilityResponseDto> CheckSubdomainAvailabilityAsync(string subdomain);

    Task<PortfolioCustomItemResponseDto> AddItemAsync(CreatePortfolioItemRequestDto dto);

    Task<PortfolioCustomItemResponseDto> UpdateItemAsync(Guid itemId, UpdatePortfolioItemRequestDto dto);

    Task RemoveItemAsync(Guid itemId);

    Task<PortfolioResponseDto> ReorderItemsAsync(ReorderPortfolioItemsRequestDto dto);

    Task<PortfolioResponseDto> SyncMyPortfolioAsync();

    Task<PublicPortfolioResponseDto> GetPublicPortfolioBySubdomainAsync(string subdomain);
}
