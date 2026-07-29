using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OboxSteam.Application.DTOs.EmailDTO;
using OboxSteam.Application.DTOs.ParentDTO;
using OboxSteam.Application.Exceptions;
using OboxSteam.Application.Interfaces;
using OboxSteam.Application.Notifications;
using OboxSteam.Application.Services;
using OboxSteam.Domain.Entities;
using OboxSteam.Domain.Enums;
using OboxSteam.Test.Helpers;

namespace OboxSteam.Test.UnitTests;

public sealed class ParentServiceTests
{
    private const string MagicToken = "ABCD123456";
    private const string ApproveToken = "XYZ9876543";
    private const string ParentEmail = "parent@test.com";
    private const string NewParentEmail = "newparent@test.com";
    private const string ShadowParentEmail = "shadow@test.com";

    private readonly Guid _studentId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private readonly Guid _parentId = Guid.Parse("14141414-1414-1414-1414-141414141414");
    private readonly Guid _parent2Id = Guid.Parse("15151515-1515-1515-1515-151515151515");
    private readonly Guid _parent3Id = Guid.Parse("16161616-1616-1616-1616-161616161616");
    private readonly Guid _managerId = Guid.Parse("13131313-1313-1313-1313-131313131313");
    private readonly Guid _magicOtpId = Guid.Parse("21212121-2121-2121-2121-212121212121");
    private readonly Guid _approveOtpId = Guid.Parse("22222222-2222-2222-2222-222222222222");

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

    private ParentService CreateSut(Guid? currentUserId = null)
    {
        _claimsService.Setup(c => c.GetCurrentUserId).Returns(currentUserId ?? _studentId);
        _emailService
            .Setup(e => e.SendMagicLinkEmailAsync(It.IsAny<ActionEmailRequestDto>()))
            .Returns(Task.CompletedTask);
        _emailService
            .Setup(e => e.SendApproveLinkEmailAsync(It.IsAny<ActionEmailRequestDto>()))
            .Returns(Task.CompletedTask);
        _notificationPublisher
            .Setup(n => n.PublishAsync(It.IsAny<NotificationCommand>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _notificationPublisher
            .Setup(n => n.PublishManyAsync(It.IsAny<IReadOnlyList<NotificationCommand>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        return new ParentService(
            _db,
            _emailService.Object,
            _claimsService.Object,
            NullLogger<ParentService>.Instance,
            _notificationPublisher.Object);
    }

    private User SeedStudent(Guid? id = null, string email = "student@test.com")
    {
        var student = new User
        {
            Id = id ?? _studentId,
            Code = "STD-001",
            Email = email,
            FullName = "Student One",
            Role = RoleType.Student,
            Status = AccountStatus.Active,
            IsDeleted = false,
        };
        _db.Users.Seed(student);
        return student;
    }

    private User SeedParent(
        Guid? id = null,
        string email = ParentEmail,
        string? passwordHash = "hashed-password",
        AccountStatus status = AccountStatus.Active)
    {
        var parent = new User
        {
            Id = id ?? _parentId,
            Code = "PRT-001",
            Email = email,
            FullName = "Parent One",
            Role = RoleType.Parent,
            PasswordHash = passwordHash,
            Status = status,
            IsDeleted = false,
        };
        _db.Users.Seed(parent);
        return parent;
    }

    private void SeedParentStudentLink(
        Guid parentId,
        Guid studentId,
        bool isVerified,
        User? parent = null,
        User? student = null)
    {
        _db.ParentStudents.Seed(new ParentStudent
        {
            ParentId = parentId,
            StudentId = studentId,
            IsVerified = isVerified,
            Parent = parent!,
            Student = student!,
            CreatedAt = DateTime.UtcNow.AddDays(-1),
            IsDeleted = false,
        });
    }

    private OtpStorage SeedOtp(
        Guid id,
        string target,
        string otpCode,
        OtpPurpose purpose,
        Guid createdBy,
        bool isUsed = false,
        DateTime? expiredAt = null)
    {
        var otp = new OtpStorage
        {
            Id = id,
            Target = target,
            OtpCode = otpCode,
            Purpose = purpose,
            CreatedBy = createdBy,
            IsUsed = isUsed,
            ExpiredAt = expiredAt ?? DateTime.UtcNow.AddHours(24),
            IsDeleted = false,
        };
        _db.OtpStorages.Seed(otp);
        return otp;
    }

    // ── RequestParentLinkAsync ────────────────────────────────────────────────

    [Fact]
    public async Task RequestParentLink_NewShadowParent_CreatesAccountPendingLinkAndMagicEmail()
    {
        SeedStudent();
        var sut = CreateSut();
        var config = CreateConfiguration();

        var result = await sut.RequestParentLinkAsync(new RequestLinkDto { ParentEmail = NewParentEmail }, config);

        Assert.True(result);
        var parent = _db.Users.Items.Single(u => u.Email == NewParentEmail);
        Assert.Equal(RoleType.Parent, parent.Role);
        Assert.Null(parent.PasswordHash);
        Assert.False(parent.IsEmailVerified);

        var link = _db.ParentStudents.Items.Single();
        Assert.Equal(parent.Id, link.ParentId);
        Assert.Equal(_studentId, link.StudentId);
        Assert.False(link.IsVerified);

        _emailService.Verify(
            e => e.SendMagicLinkEmailAsync(It.Is<ActionEmailRequestDto>(r =>
                r.To == NewParentEmail &&
                r.Link.StartsWith("https://test.example.com/magic-login"))),
            Times.Once);
        _notificationPublisher.Verify(
            n => n.PublishAsync(It.IsAny<NotificationCommand>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task RequestParentLink_ExistingRegisteredParent_SendsApproveLinkEmail()
    {
        SeedStudent();
        SeedParent(email: ParentEmail);
        var sut = CreateSut();
        var config = CreateConfiguration();

        var result = await sut.RequestParentLinkAsync(new RequestLinkDto { ParentEmail = ParentEmail }, config);

        Assert.True(result);
        Assert.Single(_db.ParentStudents.Items);
        Assert.False(_db.ParentStudents.Items[0].IsVerified);

        var otp = _db.OtpStorages.Items.Single(o => o.Purpose == OtpPurpose.ApproveLink);
        Assert.Equal(ParentEmail, otp.Target);
        Assert.Equal(_studentId, otp.CreatedBy);

        _emailService.Verify(
            e => e.SendApproveLinkEmailAsync(It.Is<ActionEmailRequestDto>(r =>
                r.To == ParentEmail &&
                r.Link.StartsWith("https://test.example.com/approve-link"))),
            Times.Once);
        _emailService.Verify(e => e.SendMagicLinkEmailAsync(It.IsAny<ActionEmailRequestDto>()), Times.Never);
    }

    [Fact]
    public async Task RequestParentLink_ExistingShadowParent_SendsMagicLinkEmail()
    {
        SeedStudent();
        SeedParent(id: _parent2Id, email: ShadowParentEmail, passwordHash: null);
        var sut = CreateSut();
        var config = CreateConfiguration();

        var result = await sut.RequestParentLinkAsync(new RequestLinkDto { ParentEmail = ShadowParentEmail }, config);

        Assert.True(result);
        Assert.Contains(_db.OtpStorages.Items, o =>
            o.Target == ShadowParentEmail && o.Purpose == OtpPurpose.MagicLink);
        _emailService.Verify(
            e => e.SendMagicLinkEmailAsync(It.Is<ActionEmailRequestDto>(r =>
                r.To == ShadowParentEmail &&
                r.Link.Contains("magic-login"))),
            Times.Once);
        _emailService.Verify(e => e.SendApproveLinkEmailAsync(It.IsAny<ActionEmailRequestDto>()), Times.Never);
    }

    [Fact]
    public async Task RequestParentLink_Throws_WhenThirdParentCapReached()
    {
        SeedStudent();
        SeedParent(id: _parentId, email: "parent1@test.com");
        SeedParent(id: _parent2Id, email: "parent2@test.com");
        SeedParent(id: _parent3Id, email: "parent3@test.com");
        SeedParentStudentLink(_parentId, _studentId, isVerified: true);
        SeedParentStudentLink(_parent2Id, _studentId, isVerified: false);
        var sut = CreateSut();

        await Assert.ThrowsAsync<BadRequestException>(() =>
            sut.RequestParentLinkAsync(new RequestLinkDto { ParentEmail = "parent3@test.com" }, CreateConfiguration()));
    }

    [Fact]
    public async Task RequestParentLink_Throws_WhenAlreadyVerified()
    {
        SeedStudent();
        var parent = SeedParent();
        SeedParentStudentLink(_parentId, _studentId, isVerified: true, parent: parent);
        var sut = CreateSut();

        await Assert.ThrowsAsync<ConflictException>(() =>
            sut.RequestParentLinkAsync(new RequestLinkDto { ParentEmail = ParentEmail }, CreateConfiguration()));
    }

    [Fact]
    public async Task RequestParentLink_Throws_WhenUnauthorized()
    {
        var sut = CreateSut(Guid.Empty);

        await Assert.ThrowsAsync<UnauthorizedException>(() =>
            sut.RequestParentLinkAsync(new RequestLinkDto { ParentEmail = NewParentEmail }, CreateConfiguration()));
    }

    // ── MagicLoginAsync ───────────────────────────────────────────────────────

    [Fact]
    public async Task MagicLogin_ValidOtp_IssuesTokensAndKeepsOtpActive()
    {
        SeedParent(email: ParentEmail, passwordHash: null);
        SeedOtp(_magicOtpId, ParentEmail, MagicToken, OtpPurpose.MagicLink, _studentId);
        var sut = CreateSut();

        var result = await sut.MagicLoginAsync(
            new MagicLoginDto { Email = ParentEmail, Token = MagicToken },
            CreateConfiguration());

        Assert.False(string.IsNullOrWhiteSpace(result.AccessToken));
        Assert.False(string.IsNullOrWhiteSpace(result.RefreshToken));

        var parent = _db.Users.Items.Single(u => u.Email == ParentEmail);
        Assert.Equal(result.RefreshToken, parent.RefreshToken);
        Assert.NotNull(parent.RefreshTokenExpiryTime);

        var otp = _db.OtpStorages.Items.Single();
        Assert.False(otp.IsUsed);
    }

    [Fact]
    public async Task MagicLogin_Throws_WhenInvalidOtp()
    {
        SeedParent(email: ParentEmail, passwordHash: null);
        SeedOtp(_magicOtpId, ParentEmail, MagicToken, OtpPurpose.MagicLink, _studentId);
        var sut = CreateSut();

        await Assert.ThrowsAsync<BadRequestException>(() =>
            sut.MagicLoginAsync(
                new MagicLoginDto { Email = ParentEmail, Token = "WRONGTOKEN" },
                CreateConfiguration()));
    }

    [Fact]
    public async Task MagicLogin_Throws_WhenExpiredOtp()
    {
        SeedParent(email: ParentEmail, passwordHash: null);
        SeedOtp(
            _magicOtpId,
            ParentEmail,
            MagicToken,
            OtpPurpose.MagicLink,
            _studentId,
            expiredAt: DateTime.UtcNow.AddMinutes(-5));
        var sut = CreateSut();

        await Assert.ThrowsAsync<BadRequestException>(() =>
            sut.MagicLoginAsync(
                new MagicLoginDto { Email = ParentEmail, Token = MagicToken },
                CreateConfiguration()));
    }

    [Fact]
    public async Task MagicLogin_Throws_WhenParentLocked()
    {
        SeedParent(email: ParentEmail, passwordHash: null, status: AccountStatus.Locked);
        SeedOtp(_magicOtpId, ParentEmail, MagicToken, OtpPurpose.MagicLink, _studentId);
        var sut = CreateSut();

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            sut.MagicLoginAsync(
                new MagicLoginDto { Email = ParentEmail, Token = MagicToken },
                CreateConfiguration()));
    }

    // ── CompleteProfileAsync ──────────────────────────────────────────────────

    [Fact]
    public async Task CompleteProfile_SetsPasswordConsumesOtpVerifiesLinkAndNotifies()
    {
        var student = SeedStudent();
        var parent = SeedParent(email: ParentEmail, passwordHash: null);
        SeedParentStudentLink(_parentId, _studentId, isVerified: false, parent: parent, student: student);
        SeedOtp(_magicOtpId, ParentEmail, MagicToken, OtpPurpose.MagicLink, _studentId);
        var sut = CreateSut(_parentId);

        var result = await sut.CompleteProfileAsync(new CompleteProfileDto
        {
            FullName = "Updated Parent",
            Phone = "0901234567",
            Password = "secret12",
        });

        Assert.True(result);

        var updatedParent = _db.Users.Items.Single(u => u.Id == _parentId);
        Assert.Equal("Updated Parent", updatedParent.FullName);
        Assert.Equal("0901234567", updatedParent.Phone);
        Assert.NotNull(updatedParent.PasswordHash);
        Assert.True(updatedParent.IsEmailVerified);

        var otp = _db.OtpStorages.Items.Single();
        Assert.True(otp.IsUsed);

        var link = _db.ParentStudents.Items.Single();
        Assert.True(link.IsVerified);

        _notificationPublisher.Verify(
            n => n.PublishManyAsync(
                It.Is<IReadOnlyList<NotificationCommand>>(cmds => cmds.Count == 2),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task CompleteProfile_Throws_WhenUnauthorized()
    {
        var sut = CreateSut(Guid.Empty);

        await Assert.ThrowsAsync<UnauthorizedException>(() =>
            sut.CompleteProfileAsync(new CompleteProfileDto
            {
                FullName = "Parent",
                Password = "secret12",
            }));
    }

    // ── ApproveLinkAsync ──────────────────────────────────────────────────────

    [Fact]
    public async Task ApproveLink_VerifiesLinkConsumesOtpAndNotifies()
    {
        var student = SeedStudent();
        var parent = SeedParent();
        SeedParentStudentLink(_parentId, _studentId, isVerified: false, parent: parent, student: student);
        SeedOtp(_approveOtpId, ParentEmail, ApproveToken, OtpPurpose.ApproveLink, _studentId);
        var sut = CreateSut(_parentId);

        var result = await sut.ApproveLinkAsync(
            new ApproveLinkDto { Token = ApproveToken },
            CreateConfiguration());

        Assert.True(result);

        var otp = _db.OtpStorages.Items.Single();
        Assert.True(otp.IsUsed);

        var link = _db.ParentStudents.Items.Single();
        Assert.True(link.IsVerified);

        _notificationPublisher.Verify(
            n => n.PublishManyAsync(
                It.Is<IReadOnlyList<NotificationCommand>>(cmds => cmds.Count == 2),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ApproveLink_Throws_WhenInvalidToken()
    {
        SeedParent();
        SeedOtp(_approveOtpId, ParentEmail, ApproveToken, OtpPurpose.ApproveLink, _studentId);
        var sut = CreateSut(_parentId);

        await Assert.ThrowsAsync<BadRequestException>(() =>
            sut.ApproveLinkAsync(new ApproveLinkDto { Token = "BADTOKEN99" }, CreateConfiguration()));
    }

    [Fact]
    public async Task ApproveLink_Throws_WhenUnauthorized()
    {
        var sut = CreateSut(Guid.Empty);

        await Assert.ThrowsAsync<UnauthorizedException>(() =>
            sut.ApproveLinkAsync(new ApproveLinkDto { Token = ApproveToken }, CreateConfiguration()));
    }

    // ── GetParentStudentRelationsAsync ──────────────────────────────────────────

    [Fact]
    public async Task GetRelations_StudentView_ReturnsLinkedParents()
    {
        var student = SeedStudent();
        var parent = SeedParent();
        SeedParentStudentLink(_parentId, _studentId, isVerified: true, parent: parent, student: student);
        var sut = CreateSut(_studentId);

        var result = await sut.GetParentStudentRelationsAsync();

        Assert.Single(result);
        Assert.Equal(_parentId, result[0].LinkedUserId);
        Assert.Equal("PRT-001", result[0].Code);
        Assert.Equal(ParentEmail, result[0].Email);
        Assert.Equal("Parent One", result[0].FullName);
        Assert.True(result[0].IsVerified);
    }

    [Fact]
    public async Task GetRelations_ParentView_ReturnsLinkedStudents()
    {
        var student = SeedStudent();
        var parent = SeedParent();
        SeedParentStudentLink(_parentId, _studentId, isVerified: true, parent: parent, student: student);
        var sut = CreateSut(_parentId);

        var result = await sut.GetParentStudentRelationsAsync();

        Assert.Single(result);
        Assert.Equal(_studentId, result[0].LinkedUserId);
        Assert.Equal("STD-001", result[0].Code);
        Assert.Equal("student@test.com", result[0].Email);
        Assert.Equal("Student One", result[0].FullName);
        Assert.True(result[0].IsVerified);
    }

    [Fact]
    public async Task GetRelations_Throws_WhenForbiddenRole()
    {
        _db.Users.Seed(new User
        {
            Id = _managerId,
            Code = "MGR-001",
            Email = "manager@test.com",
            Role = RoleType.Manager,
            IsDeleted = false,
        });
        var sut = CreateSut(_managerId);

        await Assert.ThrowsAsync<ForbiddenException>(() => sut.GetParentStudentRelationsAsync());
    }

    [Fact]
    public async Task GetRelations_Throws_WhenUnauthorized()
    {
        var sut = CreateSut(Guid.Empty);

        await Assert.ThrowsAsync<UnauthorizedException>(() => sut.GetParentStudentRelationsAsync());
    }
}
