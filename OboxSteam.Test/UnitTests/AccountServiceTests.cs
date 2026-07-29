using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OboxSteam.Application.DTOs.UserDTO;
using OboxSteam.Application.Exceptions;
using OboxSteam.Application.Interfaces;
using OboxSteam.Application.Services;
using OboxSteam.Domain.Entities;
using OboxSteam.Domain.Enums;
using OboxSteam.Test.Helpers;

namespace OboxSteam.Test.UnitTests;

public sealed class AccountServiceTests
{
    private readonly Guid _userId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private readonly Guid _otherUserId = Guid.Parse("12121212-1212-1212-1212-121212121212");

    private readonly InMemoryUnitOfWork _db = new();
    private readonly Mock<IClaimsService> _claimsService = new();
    private readonly Mock<IBlobService> _blobService = new();
    private readonly Mock<IFaceRecognitionService> _faceRecognitionService = new();

    private AccountService CreateSut(Guid? currentUserId = null)
    {
        _claimsService.Setup(c => c.GetCurrentUserId).Returns(currentUserId ?? _userId);
        _blobService
            .Setup(b => b.DeleteFileAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _blobService
            .Setup(b => b.UploadFileAsync(
                It.IsAny<string>(),
                It.IsAny<Stream>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _blobService
            .Setup(b => b.GetPreviewUrlAsync(It.IsAny<string>()))
            .ReturnsAsync("https://cdn.test/avatars/new.png");
        _faceRecognitionService
            .Setup(f => f.IndexFaceAsync(It.IsAny<Guid>(), It.IsAny<Stream>()))
            .ReturnsAsync("face-id-1");

        return new AccountService(
            _claimsService.Object,
            _db,
            _blobService.Object,
            _faceRecognitionService.Object,
            NullLogger<AccountService>.Instance);
    }

    private User SeedUser(
        Guid? id = null,
        string code = "STD-001",
        string? phone = "0900000000",
        AccountStatus status = AccountStatus.Active,
        string? avatarUrl = null,
        bool isDeleted = false)
    {
        var user = new User
        {
            Id = id ?? _userId,
            Code = code,
            Email = $"{code.ToLower()}@test.com",
            FullName = "Alice",
            Phone = phone,
            Role = RoleType.Student,
            Status = status,
            AvatarUrl = avatarUrl,
            IsEmailVerified = true,
            IsDeleted = isDeleted,
        };
        _db.Users.Seed(user);
        return user;
    }

    private static Mock<IFormFile> CreateAvatarFile(string fileName = "avatar.png", long length = 1024)
    {
        var file = new Mock<IFormFile>();
        file.Setup(f => f.FileName).Returns(fileName);
        file.Setup(f => f.Length).Returns(length);
        file.Setup(f => f.OpenReadStream()).Returns(new MemoryStream("png"u8.ToArray()));
        return file;
    }

    // ── GetCurrentUserAsync / GetUserByIdAsync ────────────────────────────────

    [Fact]
    public async Task GetCurrentUser_ReturnsProfile()
    {
        SeedUser();
        var sut = CreateSut();

        var result = await sut.GetCurrentUserAsync();

        Assert.Equal(_userId, result!.Id);
        Assert.Equal("Alice", result.FullName);
        Assert.Equal("STD-001", result.Code);
    }

    [Fact]
    public async Task GetCurrentUser_Throws_WhenMissing()
    {
        var sut = CreateSut();
        await Assert.ThrowsAsync<NotFoundException>(() => sut.GetCurrentUserAsync());
    }

    [Fact]
    public async Task GetUserById_ReturnsProfile()
    {
        SeedUser();
        var sut = CreateSut();

        var result = await sut.GetUserByIdAsync(_userId);

        Assert.Equal(_userId, result!.Id);
    }

    [Fact]
    public async Task GetUserById_Throws_WhenMissing()
    {
        var sut = CreateSut();
        await Assert.ThrowsAsync<NotFoundException>(() => sut.GetUserByIdAsync(_userId));
    }

    // ── UpdateUserProfileAsync ────────────────────────────────────────────────

    [Fact]
    public async Task UpdateProfile_UpdatesProvidedFields()
    {
        SeedUser();
        var sut = CreateSut();

        var result = await sut.UpdateUserProfileAsync(new UpdateUserDto
        {
            FullName = "  Alice Updated  ",
            Phone = "0912345678",
        });

        Assert.Equal("  Alice Updated  ", result!.FullName);
        Assert.Equal("0912345678", result.Phone);
    }

    [Fact]
    public async Task UpdateProfile_Throws_WhenPhoneTakenOrLocked()
    {
        SeedUser();
        SeedUser(_otherUserId, "STD-002", phone: "0999999999");
        var sut = CreateSut();

        await Assert.ThrowsAsync<ConflictException>(() =>
            sut.UpdateUserProfileAsync(new UpdateUserDto { Phone = "0999999999" }));

        SeedUser(id: Guid.Parse("13131313-1313-1313-1313-131313131313"), code: "STD-LOCK", status: AccountStatus.Locked);
        var lockedSut = CreateSut(Guid.Parse("13131313-1313-1313-1313-131313131313"));
        await Assert.ThrowsAsync<NotFoundException>(() =>
            lockedSut.UpdateUserProfileAsync(new UpdateUserDto { FullName = "X" }));
    }

    // ── UploadAvatarAsync ─────────────────────────────────────────────────────

    [Fact]
    public async Task UploadAvatar_UploadsAndIndexesFace()
    {
        SeedUser(avatarUrl: "https://cdn.test/old.png");
        var sut = CreateSut();

        var result = await sut.UploadAvatarAsync(CreateAvatarFile().Object);

        Assert.Equal("https://cdn.test/avatars/new.png", result!.AvatarUrl);
        _blobService.Verify(b => b.DeleteFileAsync("https://cdn.test/old.png", It.IsAny<CancellationToken>()), Times.Once);
        _faceRecognitionService.Verify(f => f.IndexFaceAsync(_userId, It.IsAny<Stream>()), Times.Once);
        _blobService.Verify(
            b => b.UploadFileAsync(
                It.Is<string>(n => n.StartsWith($"{_userId}_")),
                It.IsAny<Stream>(),
                "avatars",
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task UploadAvatar_Throws_WhenInvalidFileOrMissingUser()
    {
        SeedUser();
        var sut = CreateSut();

        await Assert.ThrowsAsync<BadRequestException>(() =>
            sut.UploadAvatarAsync(CreateAvatarFile("doc.pdf").Object));
        await Assert.ThrowsAsync<BadRequestException>(() =>
            sut.UploadAvatarAsync(CreateAvatarFile(length: 6 * 1024 * 1024).Object));

        var missingSut = CreateSut(Guid.Parse("99999999-9999-9999-9999-999999999999"));
        await Assert.ThrowsAsync<NotFoundException>(() =>
            missingSut.UploadAvatarAsync(CreateAvatarFile().Object));
    }
}
