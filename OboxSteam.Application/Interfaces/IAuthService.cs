using Microsoft.Extensions.Configuration;
using OboxSteam.Application.DTOs.AuthDTO;
using OboxSteam.Application.DTOs.UserDTO;
using OboxSteam.Domain.Enums;

namespace OboxSteam.Application.Interfaces;

public interface IAuthService
{
    Task<UserDto?> RegisterUserAsync(UserRegistrationDto registrationDto);

    /// <summary>IConfiguration is injected into the service — no longer passed per-call.</summary>
    Task<LoginResponseDto?> LoginAsync(LoginRequestDto loginDto, IConfiguration configuration);

    Task<bool> LogoutAsync();

    /// <summary>IConfiguration is injected into the service — no longer passed per-call.</summary>
    Task<LoginResponseDto?> RefreshTokenAsync(TokenRefreshRequestDto refreshTokenDto, IConfiguration configuration);
    Task<bool> VerifyEmailOtpAsync(string email, string otp);

    Task<bool> ResendOtpAsync(string email, OtpPurpose otpPurpose);

    Task<bool> ResetPasswordAsync(string email, string otp, string newPassword);
}