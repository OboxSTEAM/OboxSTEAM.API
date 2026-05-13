using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using OboxSteam.Application.DTOs.AuthDTO;
using OboxSteam.Application.DTOs.EmailDTO;
using OboxSteam.Application.DTOs.UserDTO;
using OboxSteam.Application.Interfaces;
using OboxSteam.Application.Utils;
using OboxSteam.Domain.Entities;
using OboxSteam.Domain.Enums;
using OboxSteam.Domain.Interfaces;

namespace OboxSteam.Application.Services;

public class AuthService : IAuthService
{
    private readonly IEmailService _emailService;
    private readonly ILogger<AuthService> _logger;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClaimsService _claimsService;
    private readonly IConfiguration _configuration;

    public AuthService(
        IUnitOfWork unitOfWork,
        IEmailService emailService,
        ILogger<AuthService> logger,
        IClaimsService claimsService,
        IConfiguration configuration)
    {
        _unitOfWork = unitOfWork;
        _emailService = emailService;
        _logger = logger;
        _claimsService = claimsService;
        _configuration = configuration;
    }

    /// <summary>Register a new user.</summary>
    public async Task<UserDto?> RegisterUserAsync(UserRegistrationDto registrationDto)
    {
        _logger.LogInformation("Start registration for {Email}", registrationDto.Email);

        if (await UserExistsAsync(registrationDto.Email))
        {
            _logger.LogWarning("Email {Email} already registered.", registrationDto.Email);
            throw ErrorHelper.Conflict("Email is already in use.");
        }

        var hashedPassword = new PasswordHasher().HashPassword(registrationDto.Password);

        var user = new User
        {
            Email = registrationDto.Email,
            Username = registrationDto.Username,
            PasswordHash = hashedPassword,
            Gender = registrationDto.Gender ?? false,
            IsEmailVerified = false
        };

        await _unitOfWork.Users.AddAsync(user);
        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation("User {Email} created successfully.", user.Email);

        await GenerateAndSendOtpAsync(user, OtpPurpose.Register);

        _logger.LogInformation("OTP sent to {Email} for verification.", user.Email);

        return new UserDto
        {
            Id = user.Id,
            Username = user.Username,
            Email = user.Email,
            AvatarUrl = user.AvatarUrl,
            Gender = user.Gender,
            PaymentQrCodeUrl = user.PaymentQrCodeUrl,
            IsEmailVerified = user.IsEmailVerified,
            CreatedAt = user.CreatedAt
        };
    }

    /// <summary>Login a user and return JWT access and refresh token.</summary>
    public async Task<LoginResponseDto?> LoginAsync(LoginRequestDto loginDto)
    {
        _logger.LogInformation("Login attempt for {Email}", loginDto.Email);

        var user = await _unitOfWork.Users.FirstOrDefaultAsync(u => u.Email == loginDto.Email && !u.IsDeleted);

        if (user == null)
            throw ErrorHelper.NotFound("Account does not exist.");

        if (!new PasswordHasher().VerifyPassword(loginDto.Password!, user.PasswordHash))
            throw ErrorHelper.Unauthorized("Incorrect password.");

        if (user.IsDeleted)
            throw ErrorHelper.Forbidden("Your account has been disabled. Please contact support.");

        if (!user.IsEmailVerified)
            throw ErrorHelper.Forbidden("Please verify your email before logging in.");

        _logger.LogInformation("User {Email} authenticated successfully.", loginDto.Email);

        var accessToken = JwtUtils.GenerateJwtToken(
            user.Id,
            user.Email,
            "User",
            _configuration,
            TimeSpan.FromMinutes(30));

        var refreshToken = TokenTools.GenerateRefreshToken();
        user.RefreshToken = refreshToken;
        user.RefreshTokenExpiryTime = DateTime.UtcNow.AddDays(7);

        await _unitOfWork.Users.Update(user);
        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation("Tokens generated for {Email}.", user.Email);

        return new LoginResponseDto
        {
            AccessToken = accessToken,
            RefreshToken = refreshToken,
        };
    }

    /// <summary>Logout a user by clearing their refresh token.</summary>
    public async Task<bool> LogoutAsync()
    {
        var userId = _claimsService.GetCurrentUserId;
        _logger.LogInformation("Logout initiated for user {UserId}", userId);

        var user = await _unitOfWork.Users.FirstOrDefaultAsync(u => u.Id == userId && !u.IsDeleted);

        if (user == null)
            throw ErrorHelper.NotFound("Account does not exist.");

        if (user.IsDeleted)
            throw ErrorHelper.Forbidden("Account has been disabled.");

        if (string.IsNullOrEmpty(user.RefreshToken))
            throw ErrorHelper.BadRequest("User is already logged out.");

        user.RefreshToken = null;
        user.RefreshTokenExpiryTime = null;
        await _unitOfWork.Users.Update(user);
        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation("Logout successful for user {UserId}.", userId);
        return true;
    }

    /// <summary>Refresh the access token using the refresh token.</summary>
    public async Task<LoginResponseDto?> RefreshTokenAsync(TokenRefreshRequestDto refreshTokenDto)
    {
        if (string.IsNullOrWhiteSpace(refreshTokenDto.RefreshToken))
            throw ErrorHelper.BadRequest("Refresh token is required.");

        var user = await _unitOfWork.Users.FirstOrDefaultAsync(u =>
            u.RefreshToken == refreshTokenDto.RefreshToken && !u.IsDeleted);

        if (user == null)
            throw ErrorHelper.NotFound("Account does not exist.");

        if (string.IsNullOrEmpty(user.RefreshToken))
            throw ErrorHelper.BadRequest("User is already logged out.");

        if (user.RefreshTokenExpiryTime < DateTime.UtcNow)
            throw ErrorHelper.Unauthorized("Refresh token has expired. Please log in again.");

        var newAccessToken = JwtUtils.GenerateJwtToken(
            user.Id,
            user.Email,
            "User",
            _configuration,
            TimeSpan.FromHours(1));

        var newRefreshToken = TokenTools.GenerateRefreshToken();
        user.RefreshToken = newRefreshToken;
        user.RefreshTokenExpiryTime = DateTime.UtcNow.AddDays(7);

        await _unitOfWork.Users.Update(user);
        await _unitOfWork.SaveChangesAsync();

        return new LoginResponseDto
        {
            AccessToken = newAccessToken,
            RefreshToken = newRefreshToken
        };
    }

    /// <summary>Verify email OTP to activate an account.</summary>
    public async Task<bool> VerifyEmailOtpAsync(string email, string otp)
    {
        _logger.LogInformation("Verifying OTP for {Email}", email);

        var user = await _unitOfWork.Users.FirstOrDefaultAsync(u => u.Email == email);
        if (user == null) throw ErrorHelper.NotFound("Account does not exist.");

        if (user.IsEmailVerified) return false;
        if (!await VerifyOtpAsync(email, otp, OtpPurpose.Register)) return false;

        user.IsEmailVerified = true;
        _logger.LogInformation("OTP verified for {Email}, activating account.", email);

        await _unitOfWork.Users.Update(user);
        await _unitOfWork.SaveChangesAsync();

        await _emailService.SendRegistrationSuccessEmailAsync(new EmailRequestDto
        {
            To = user.Email,
            UserName = user.Username
        });

        _logger.LogInformation("User {Email} verified and activated.", email);
        return true;
    }

    /// <summary>Resend an OTP for registration or forgot-password flows.</summary>
    public async Task<bool> ResendOtpAsync(string email, OtpPurpose otpPurpose)
    {
        return otpPurpose switch
        {
            OtpPurpose.Register        => await SendRegisterOtpAsync(email),
            OtpPurpose.ForgotPassword  => await SendForgotPasswordOtpAsync(email),
            _                          => throw ErrorHelper.BadRequest("Invalid OTP type.")
        };
    }

    /// <summary>Reset a user's password using an OTP.</summary>
    public async Task<bool> ResetPasswordAsync(string email, string otp, string newPassword)
    {
        _logger.LogInformation("Password reset requested for {Email}", email);

        var user = await _unitOfWork.Users.FirstOrDefaultAsync(u => u.Email == email && !u.IsDeleted);
        if (user == null) return false;
        if (!user.IsEmailVerified) return false;
        if (!await VerifyOtpAsync(email, otp, OtpPurpose.ForgotPassword)) return false;

        user.PasswordHash = new PasswordHasher().HashPassword(newPassword);
        await _unitOfWork.Users.Update(user);
        await _unitOfWork.SaveChangesAsync();

        await _emailService.SendPasswordChangeSuccessAsync(new EmailRequestDto
        {
            To = user.Email,
            UserName = user.Username
        });

        _logger.LogInformation("Password reset successful for {Email}.", email);
        return true;
    }

    //========================= PRIVATE HELPERS =================================

    private async Task<bool> UserExistsAsync(string email)
    {
        var user = await _unitOfWork.Users.FirstOrDefaultAsync(u => u.Email == email);
        return user != null;
    }

    private async Task GenerateAndSendOtpAsync(User user, OtpPurpose purpose)
    {
        var otpToken = OtpGenerator.GenerateToken(6, TimeSpan.FromMinutes(10));
        var otp = new OtpStorage
        {
            Target = user.Email,
            OtpCode = otpToken.Code,
            ExpiredAt = otpToken.ExpiresAtUtc,
            IsUsed = false,
            Purpose = purpose
        };

        await _unitOfWork.OtpStorages.AddAsync(otp);
        await _unitOfWork.SaveChangesAsync();

        var emailRequest = new EmailRequestDto
        {
            To = user.Email,
            Otp = otpToken.Code,
            UserName = user.Username
        };

        if (purpose == OtpPurpose.Register)
        {
            await _emailService.SendOtpVerificationEmailAsync(emailRequest);
            _logger.LogInformation("Registration OTP sent to {Email}", user.Email);
        }
        else if (purpose == OtpPurpose.ForgotPassword)
        {
            await _emailService.SendForgotPasswordOtpEmailAsync(emailRequest);
            _logger.LogInformation("Forgot-password OTP sent to {Email}", user.Email);
        }
    }

    private async Task<bool> SendRegisterOtpAsync(string email)
    {
        var user = await _unitOfWork.Users.FirstOrDefaultAsync(u => u.Email == email);
        if (user == null)
            throw ErrorHelper.NotFound("Email does not exist in the system.");

        if (user.IsDeleted)
            throw ErrorHelper.Forbidden("Account has been disabled.");

        if (user.IsEmailVerified)
            throw ErrorHelper.Conflict("Account is already verified.");

        await GenerateAndSendOtpAsync(user, OtpPurpose.Register);
        return true;
    }

    private async Task<bool> SendForgotPasswordOtpAsync(string email)
    {
        var user = await _unitOfWork.Users.FirstOrDefaultAsync(u => u.Email == email);
        if (user == null)
            throw ErrorHelper.NotFound("Email does not exist in the system.");

        if (user.IsDeleted)
            throw ErrorHelper.Forbidden("Account has been disabled.");

        await GenerateAndSendOtpAsync(user, OtpPurpose.ForgotPassword);
        return true;
    }

    private async Task<bool> VerifyOtpAsync(string email, string otp, OtpPurpose purpose)
    {
        var otpRecord = await _unitOfWork.OtpStorages.FirstOrDefaultAsync(o =>
            o.Target == email && o.OtpCode == otp && o.Purpose == purpose && !o.IsUsed);

        if (otpRecord == null || otpRecord.ExpiredAt < DateTime.UtcNow)
        {
            _logger.LogWarning("OTP not found or expired for {Email} (purpose: {Purpose})", email, purpose);
            return false;
        }

        otpRecord.IsUsed = true;
        await _unitOfWork.OtpStorages.Update(otpRecord);
        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation("OTP for {Email} (purpose: {Purpose}) verified and marked as used.", email, purpose);
        return true;
    }
}