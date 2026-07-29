using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OboxSteam.Application.DTOs.AuthDTO;
using OboxSteam.Application.DTOs.EmailDTO;
using OboxSteam.Application.Exceptions;
using OboxSteam.Application.Interfaces;
using OboxSteam.Application.Notifications;
using OboxSteam.Application.Services;
using OboxSteam.Application.Utils;
using OboxSteam.Domain.Entities;
using OboxSteam.Domain.Enums;
using OboxSteam.Test.Helpers;

namespace OboxSteam.Test.UnitTests;

public sealed class AuthServiceTests
{
    private const string Password = "Secret123!";
    private const string Email = "user@test.com";
    private const string RegisterOtp = "123456";
    private const string ResetToken = "RESETTOKEN1";

    private readonly Guid _userId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    private readonly InMemoryUnitOfWork _db = new();
    private readonly Mock<IEmailService> _emailService = new();
    private readonly Mock<IClaimsService> _claimsService = new();
    private readonly Mock<INotificationPublisher> _notificationPublisher = new();

    private static IConfiguration CreateConfiguration() =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["JWT:SecretKey"] = "this-is-a-test-secret-key-32chars!",
                ["JWT:Issuer"] = "test",
                ["JWT:Audience"] = "test",
                ["APP_BASE_URL"] = "https://test.example.com",
            })
            .Build();

    private AuthService CreateSut(Guid? currentUserId = null)
    {
        _claimsService.Setup(c => c.GetCurrentUserId).Returns(currentUserId ?? _userId);
        _emailService
            .Setup(e => e.SendOtpVerificationEmailAsync(It.IsAny<EmailRequestDto>()))
            .Returns(Task.CompletedTask);
        _emailService
            .Setup(e => e.SendRegistrationSuccessEmailAsync(It.IsAny<EmailRequestDto>()))
            .Returns(Task.CompletedTask);
        _emailService
            .Setup(e => e.SendForgotPasswordLinkEmailAsync(It.IsAny<ActionEmailRequestDto>()))
            .Returns(Task.CompletedTask);
        _emailService
            .Setup(e => e.SendPasswordChangeSuccessAsync(It.IsAny<EmailRequestDto>()))
            .Returns(Task.CompletedTask);
        _notificationPublisher
            .Setup(n => n.PublishAsync(It.IsAny<NotificationCommand>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        return new AuthService(
            _db,
            _emailService.Object,
            NullLogger<AuthService>.Instance,
            _claimsService.Object,
            CreateConfiguration(),
            _notificationPublisher.Object);
    }

    private User SeedUser(
        Guid? id = null,
        string? email = null,
        RoleType role = RoleType.Student,
        bool isEmailVerified = true,
        AccountStatus status = AccountStatus.Active,
        string? refreshToken = "refresh-token",
        DateTime? refreshExpiry = null,
        bool isDeleted = false)
    {
        var user = new User
        {
            Id = id ?? _userId,
            Code = "STD-001",
            Email = email ?? Email,
            FullName = "Test User",
            Phone = "0900000000",
            PasswordHash = new PasswordHasher().HashPassword(Password),
            Role = role,
            Status = status,
            IsEmailVerified = isEmailVerified,
            RefreshToken = refreshToken,
            RefreshTokenExpiryTime = refreshExpiry ?? DateTime.UtcNow.AddDays(7),
            IsDeleted = isDeleted,
        };
        _db.Users.Seed(user);
        return user;
    }

    // ── RegisterUserAsync ─────────────────────────────────────────────────────

    [Fact]
    public async Task RegisterUser_CreatesStudentWithProfileAndSendsOtp()
    {
        var sut = CreateSut();

        var result = await sut.RegisterUserAsync(new UserRegistrationDto
        {
            Email = "newstudent@test.com",
            Password = Password,
            FullName = "New Student",
            Phone = "0911111111",
            Role = RoleType.Student,
        });

        Assert.NotNull(result);
        Assert.Equal(RoleType.Student, result!.Role);
        Assert.False(result.IsEmailVerified);
        Assert.Single(_db.Users.Items);
        Assert.NotNull(_db.Users.Items[0].StudentProfile);
        _emailService.Verify(e => e.SendOtpVerificationEmailAsync(It.IsAny<EmailRequestDto>()), Times.Once);
        _notificationPublisher.Verify(
            n => n.PublishAsync(It.IsAny<NotificationCommand>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task RegisterUser_Throws_WhenInvalidRoleOrDuplicateEmail()
    {
        SeedUser(email: Email);
        var sut = CreateSut();

        await Assert.ThrowsAsync<BadRequestException>(() =>
            sut.RegisterUserAsync(new UserRegistrationDto
            {
                Email = "mgr@test.com",
                Password = Password,
                FullName = "Manager",
                Role = RoleType.Manager,
            }));

        await Assert.ThrowsAsync<ConflictException>(() =>
            sut.RegisterUserAsync(new UserRegistrationDto
            {
                Email = Email,
                Password = Password,
                FullName = "Dup",
                Role = RoleType.Parent,
            }));
    }

    // ── LoginAsync ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Login_ReturnsTokens_WhenCredentialsValid()
    {
        SeedUser();
        var sut = CreateSut();
        var config = CreateConfiguration();

        var result = await sut.LoginAsync(new LoginRequestDto
        {
            Email = Email,
            Password = Password,
        }, config);

        Assert.False(string.IsNullOrWhiteSpace(result!.AccessToken));
        Assert.False(string.IsNullOrWhiteSpace(result.RefreshToken));
        Assert.NotNull(_db.Users.Items[0].RefreshToken);
    }

    [Fact]
    public async Task Login_Throws_WhenInvalidCredentialsOrBlocked()
    {
        SeedUser(isEmailVerified: false);
        var sut = CreateSut();
        var config = CreateConfiguration();

        await Assert.ThrowsAsync<UnauthorizedException>(() =>
            sut.LoginAsync(new LoginRequestDto { Email = Email, Password = "wrong" }, config));

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            sut.LoginAsync(new LoginRequestDto { Email = Email, Password = Password }, config));

        SeedUser(
            id: Guid.Parse("12121212-1212-1212-1212-121212121212"),
            email: "locked@test.com",
            status: AccountStatus.Locked);
        await Assert.ThrowsAsync<ForbiddenException>(() =>
            sut.LoginAsync(new LoginRequestDto { Email = "locked@test.com", Password = Password }, config));

        await Assert.ThrowsAsync<NotFoundException>(() =>
            sut.LoginAsync(new LoginRequestDto { Email = "missing@test.com", Password = Password }, config));
    }

    // ── LogoutAsync ─────────────────────────────────────────────────────────

    [Fact]
    public async Task Logout_ClearsRefreshToken()
    {
        SeedUser();
        var sut = CreateSut();

        Assert.True(await sut.LogoutAsync());
        Assert.Null(_db.Users.Items[0].RefreshToken);
    }

    [Fact]
    public async Task Logout_Throws_WhenAlreadyLoggedOutOrMissing()
    {
        SeedUser(refreshToken: null, refreshExpiry: null);
        var sut = CreateSut();

        await Assert.ThrowsAsync<BadRequestException>(() => sut.LogoutAsync());

        var missingSut = CreateSut(Guid.Parse("99999999-9999-9999-9999-999999999999"));
        await Assert.ThrowsAsync<NotFoundException>(() => missingSut.LogoutAsync());
    }

    // ── RefreshTokenAsync ─────────────────────────────────────────────────────

    [Fact]
    public async Task RefreshToken_ReturnsNewTokens()
    {
        SeedUser(refreshToken: "old-refresh");
        var sut = CreateSut();
        var config = CreateConfiguration();

        var result = await sut.RefreshTokenAsync(
            new TokenRefreshRequestDto { RefreshToken = "old-refresh" },
            config);

        Assert.False(string.IsNullOrWhiteSpace(result!.AccessToken));
        Assert.NotEqual("old-refresh", result.RefreshToken);
    }

    [Fact]
    public async Task RefreshToken_Throws_WhenInvalidOrExpired()
    {
        SeedUser(refreshToken: "valid");
        var sut = CreateSut();
        var config = CreateConfiguration();

        await Assert.ThrowsAsync<BadRequestException>(() =>
            sut.RefreshTokenAsync(new TokenRefreshRequestDto { RefreshToken = "" }, config));
        await Assert.ThrowsAsync<NotFoundException>(() =>
            sut.RefreshTokenAsync(new TokenRefreshRequestDto { RefreshToken = "missing" }, config));

        SeedUser(
            id: Guid.Parse("13131313-1313-1313-1313-131313131313"),
            email: "expired@test.com",
            refreshToken: "expired",
            refreshExpiry: DateTime.UtcNow.AddDays(-1));
        await Assert.ThrowsAsync<UnauthorizedException>(() =>
            sut.RefreshTokenAsync(new TokenRefreshRequestDto { RefreshToken = "expired" }, config));
    }

    // ── VerifyEmailOtpAsync / ResendOtpAsync ──────────────────────────────────

    [Fact]
    public async Task VerifyEmailOtp_ActivatesAccount()
    {
        SeedUser(isEmailVerified: false);
        _db.OtpStorages.Seed(new OtpStorage
        {
            Id = Guid.NewGuid(),
            Target = Email,
            OtpCode = RegisterOtp,
            Purpose = OtpPurpose.Register,
            ExpiredAt = DateTime.UtcNow.AddMinutes(10),
            IsUsed = false,
        });
        var sut = CreateSut();

        Assert.True(await sut.VerifyEmailOtpAsync(Email, RegisterOtp));
        Assert.True(_db.Users.Items[0].IsEmailVerified);
        Assert.True(_db.OtpStorages.Items[0].IsUsed);
        _emailService.Verify(e => e.SendRegistrationSuccessEmailAsync(It.IsAny<EmailRequestDto>()), Times.Once);
    }

    [Fact]
    public async Task VerifyEmailOtp_Throws_WhenAlreadyVerified()
    {
        SeedUser();
        var sut = CreateSut();

        await Assert.ThrowsAsync<ConflictException>(() => sut.VerifyEmailOtpAsync(Email, RegisterOtp));
    }

    [Fact]
    public async Task VerifyEmailOtp_Throws_WhenInvalidOtp()
    {
        SeedUser(isEmailVerified: false);
        var sut = CreateSut();

        await Assert.ThrowsAsync<BadRequestException>(() =>
            sut.VerifyEmailOtpAsync(Email, "000000"));
    }

    [Fact]
    public async Task ResendOtp_SendsRegisterOtp()
    {
        SeedUser(isEmailVerified: false);
        var sut = CreateSut();

        Assert.True(await sut.ResendOtpAsync(Email, OtpPurpose.Register));
        _emailService.Verify(e => e.SendOtpVerificationEmailAsync(It.IsAny<EmailRequestDto>()), Times.Once);
    }

    [Fact]
    public async Task ResendOtp_Throws_WhenInvalidPurposeOrAlreadyVerified()
    {
        SeedUser(isEmailVerified: false);
        var sut = CreateSut();

        await Assert.ThrowsAsync<BadRequestException>(() =>
            sut.ResendOtpAsync(Email, OtpPurpose.ForgotPassword));
        await Assert.ThrowsAsync<NotFoundException>(() =>
            sut.ResendOtpAsync("missing@test.com", OtpPurpose.Register));

        SeedUser(
            id: Guid.Parse("14141414-1414-1414-1414-141414141414"),
            email: "verified@test.com");
        await Assert.ThrowsAsync<ConflictException>(() =>
            sut.ResendOtpAsync("verified@test.com", OtpPurpose.Register));
    }

    // ── ForgotPasswordAsync / ResetPasswordAsync ──────────────────────────────

    [Fact]
    public async Task ForgotPassword_SendsResetLink()
    {
        SeedUser();
        var sut = CreateSut();

        Assert.True(await sut.ForgotPasswordAsync(Email));
        _emailService.Verify(e => e.SendForgotPasswordLinkEmailAsync(It.IsAny<ActionEmailRequestDto>()), Times.Once);
        Assert.Single(_db.OtpStorages.Items, o => o.Purpose == OtpPurpose.ForgotPassword);
    }

    [Fact]
    public async Task ForgotPassword_Throws_WhenEmailMissing()
    {
        var sut = CreateSut();
        await Assert.ThrowsAsync<NotFoundException>(() => sut.ForgotPasswordAsync("missing@test.com"));
    }

    [Fact]
    public async Task ResetPassword_UpdatesHashAndConsumesToken()
    {
        SeedUser();
        _db.OtpStorages.Seed(new OtpStorage
        {
            Id = Guid.NewGuid(),
            Target = Email,
            OtpCode = ResetToken,
            Purpose = OtpPurpose.ForgotPassword,
            ExpiredAt = DateTime.UtcNow.AddMinutes(15),
            IsUsed = false,
        });
        var sut = CreateSut();

        Assert.True(await sut.ResetPasswordAsync(ResetToken, "NewPassword1!"));
        Assert.True(new PasswordHasher().VerifyPassword("NewPassword1!", _db.Users.Items[0].PasswordHash!));
        Assert.True(_db.OtpStorages.Items[0].IsUsed);
        _emailService.Verify(e => e.SendPasswordChangeSuccessAsync(It.IsAny<EmailRequestDto>()), Times.Once);
    }

    [Fact]
    public async Task ResetPassword_Throws_WhenTokenInvalidOrEmailUnverified()
    {
        var sut = CreateSut();
        await Assert.ThrowsAsync<BadRequestException>(() => sut.ResetPasswordAsync("bad", "NewPassword1!"));

        SeedUser(isEmailVerified: false);
        _db.OtpStorages.Seed(new OtpStorage
        {
            Id = Guid.NewGuid(),
            Target = Email,
            OtpCode = ResetToken,
            Purpose = OtpPurpose.ForgotPassword,
            ExpiredAt = DateTime.UtcNow.AddMinutes(15),
            IsUsed = false,
        });
        await Assert.ThrowsAsync<ForbiddenException>(() => sut.ResetPasswordAsync(ResetToken, "NewPassword1!"));
    }
}
