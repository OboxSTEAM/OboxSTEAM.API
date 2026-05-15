using Microsoft.AspNetCore.Http;
using OboxSteam.Application.DTOs.UserDTO;

namespace OboxSteam.Application.Interfaces;

public interface IAccountService
{
    Task<UserDto?> GetCurrentUserAsync();
    Task<UserDto?> GetUserByIdAsync(Guid userId);
    Task<UserDto?> UpdateUserProfileAsync(UpdateUserDto updateUserDto);
    Task<UserDto?> UploadAvatarAsync(IFormFile file);
}