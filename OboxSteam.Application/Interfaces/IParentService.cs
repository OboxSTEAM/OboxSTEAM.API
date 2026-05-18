using Microsoft.Extensions.Configuration;
using OboxSteam.Application.DTOs.AuthDTO; 
using OboxSteam.Application.DTOs.ParentDTO;

namespace OboxSteam.Application.Interfaces;

public interface IParentService
{
    Task<bool> RequestParentLinkAsync(RequestLinkDto dto, IConfiguration configuration);
    Task<LoginResponseDto> MagicLoginAsync(MagicLoginDto dto, IConfiguration configuration);
    Task<bool> CompleteProfileAsync(CompleteProfileDto dto);
    Task<bool> ApproveLinkAsync(ApproveLinkDto dto, IConfiguration configuration);
}
