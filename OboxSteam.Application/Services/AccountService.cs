using Microsoft.AspNetCore.Http;
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
    private readonly IBlobService _blobService;
    private readonly IFaceRecognitionService _faceRecognitionService;

    public AccountService(IClaimsService claimsService,
                          IUnitOfWork unitOfWork,
                          IBlobService blobService,
                          IFaceRecognitionService faceRecognitionService,
                          ILogger<AccountService> logger)
    {
        _claimsService = claimsService;
        _unitOfWork = unitOfWork;
        _blobService = blobService;
        _faceRecognitionService = faceRecognitionService;
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

    /// <summary>
    /// Upload avatar for the current authenticated user.
    /// Deletes the old avatar (if any) before uploading the new one.
    /// </summary>
    public async Task<UserDto?> UploadAvatarAsync(IFormFile file)
    {
        var userId = _claimsService.GetCurrentUserId;
        _loggerService.LogInformation("Uploading avatar for user ID: {UserId}", userId);

        // Validate file
        var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif" };
        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();

        if (!allowedExtensions.Contains(extension))
            throw ErrorHelper.BadRequest("Only image files (.jpg, .jpeg, .png, .gif) are allowed.");

        if (file.Length > 5 * 1024 * 1024) // 5 MB limit
            throw ErrorHelper.BadRequest("Avatar file size must not exceed 5 MB.");

        var user = await _unitOfWork.Users.FirstOrDefaultAsync(u => u.Id == userId && !u.IsDeleted);

        if (user == null)
            throw ErrorHelper.NotFound("Account does not exist.");

        // Delete old avatar if exists
        if (!string.IsNullOrWhiteSpace(user.AvatarUrl))
        {
            _loggerService.LogInformation("Deleting old avatar for user {UserId}", userId);
            await _blobService.DeleteFileAsync(user.AvatarUrl);
        }

        await using var faceStream = file.OpenReadStream();
        await _faceRecognitionService.IndexFaceAsync(userId, faceStream);
        _loggerService.LogInformation("Face indexed in Rekognition for user {UserId}", userId);

        // Generate unique file name: avatars/{userId}_{timestamp}{ext}
        var fileName = $"{userId}_{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}{extension}";

        await using var stream = file.OpenReadStream();
        await _blobService.UploadFileAsync(fileName, stream, "avatars");

        // Get the preview URL and save to user
        var avatarUrl = await _blobService.GetPreviewUrlAsync($"avatars/{fileName}");
        user.AvatarUrl = avatarUrl;

        await _unitOfWork.Users.Update(user);
        await _unitOfWork.SaveChangesAsync();

        _loggerService.LogInformation("Avatar uploaded successfully for user ID: {UserId}", userId);

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
