using Microsoft.Extensions.Logging;
using OboxSteam.Application.DTOs.UserDTO;
using OboxSteam.Application.Interfaces;
using OboxSteam.Application.Utils;
using OboxSteam.Domain.Enums;
using OboxSteam.Domain.Interfaces;

namespace OboxSteam.Application.Services;

public class AccountService : IAccountService
{
    private readonly IClaimsService _claimsService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger _loggerService;

    public AccountService(IClaimsService claimsService,
                          IUnitOfWork unitOfWork,
                          ILogger<AccountService> logger)
    {
        _claimsService = claimsService;
        _unitOfWork = unitOfWork;
        _loggerService = logger;
    }

    /// <summary>
    /// Get current user profile
    /// </summary>
    public async Task<UserDto?> GetCurrentUserAsync()
    {
        var userId = _claimsService.GetCurrentUserId;
        _loggerService.LogInformation("Getting profile for user ID: {UserId}", userId);

        var user = await _unitOfWork.Users.FirstOrDefaultAsync(u => u.Id == userId && !u.IsDeleted);

        if (user == null)
        {
            _loggerService.LogWarning($"User with ID {userId} not found");
            throw ErrorHelper.NotFound("Account does not exist.");
        }

        return new UserDto
        {
            Id = user.Id,
            Code = user.Code,
            FullName = user.FullName,
            Email = user.Email,
            AvatarUrl = user.AvatarUrl,
            Phone = user.Phone,
            Role = user.Role,
            Status = user.Status,
            IsEmailVerified = user.IsEmailVerified,
            CreatedAt = user.CreatedAt
        };
    }

    /// <summary>
    /// Get user by ID
    /// </summary>
    public async Task<UserDto?> GetUserByIdAsync(Guid userId)
    {
        _loggerService.LogInformation("Getting profile for user ID: {UserId}", userId);

        var user = await _unitOfWork.Users.FirstOrDefaultAsync(u => u.Id == userId && !u.IsDeleted);

        if (user == null)
        {
            _loggerService.LogWarning("User with ID {UserId} not found", userId);
            throw ErrorHelper.NotFound("Account does not exist.");
        }

        return new UserDto
        {
            Id = user.Id,
            Code = user.Code,
            FullName = user.FullName,
            Email = user.Email,
            AvatarUrl = user.AvatarUrl,
            Phone = user.Phone,
            Role = user.Role,
            Status = user.Status,
            IsEmailVerified = user.IsEmailVerified,
            CreatedAt = user.CreatedAt
        };
    }
    /// <summary>
    /// Update current user profile
    /// </summary>
    public async Task<UserDto?> UpdateUserProfileAsync(UpdateUserDto updateUserDto)
    {
        var userId = _claimsService.GetCurrentUserId;
        _loggerService.LogInformation("Updating profile for user ID: {UserId}", userId);

        var user = await _unitOfWork.Users.FirstOrDefaultAsync(u => u.Id == userId && !u.IsDeleted && u.Status != AccountStatus.Locked);

        if (user == null)
        {
            _loggerService.LogWarning($"User with ID {userId} not found");
            throw ErrorHelper.NotFound("Account does not exist.");
        }

        // Update only provided fields
        if (!string.IsNullOrWhiteSpace(updateUserDto.FullName))
        {
            user.FullName = updateUserDto.FullName;
        }

        if (!string.IsNullOrWhiteSpace(updateUserDto.Phone))
        {
            // Check if phone is already taken
            var existingUser = await _unitOfWork.Users.FirstOrDefaultAsync(u =>
                u.Phone == updateUserDto.Phone && u.Id != userId && !u.IsDeleted);

            if (existingUser != null)
            {
                _loggerService.LogWarning("Phone {Phone} is already taken", updateUserDto.Phone);
                throw ErrorHelper.Conflict("Phone is already taken.");
            }

            user.Phone = updateUserDto.Phone;
        }

        await _unitOfWork.Users.Update(user);
        await _unitOfWork.SaveChangesAsync();

        _loggerService.LogInformation("Profile updated successfully for user ID: {UserId}", userId);

        return new UserDto
        {
            Id = user.Id,
            Code = user.Code,
            FullName = user.FullName,
            Email = user.Email,
            AvatarUrl = user.AvatarUrl,
            Phone = user.Phone,
            Role = user.Role,
            Status = user.Status,
            IsEmailVerified = user.IsEmailVerified,
            CreatedAt = user.CreatedAt
        };
    }

}