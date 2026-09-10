using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OboxSteam.Application.DTOs.VoucherDTO;
using OboxSteam.Application.Exceptions;
using OboxSteam.Application.Interfaces;
using OboxSteam.Application.Services;
using OboxSteam.Domain.Entities;
using OboxSteam.Domain.Enums;
using OboxSteam.Test.Helpers;

namespace OboxSteam.Test.UnitTests;

public sealed class VoucherServiceTests
{
    private readonly Guid _studentId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private readonly Guid _parentId = Guid.Parse("14141414-1414-1414-1414-141414141414");
    private readonly Guid _managerId = Guid.Parse("13131313-1313-1313-1313-131313131313");
    private readonly Guid _programAId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private readonly Guid _programBId = Guid.Parse("23232323-2323-2323-2323-232323232323");
    private readonly Guid _bundleId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private readonly DateTime _now = new(2026, 9, 10, 0, 0, 0, DateTimeKind.Utc);

    private readonly InMemoryUnitOfWork _db = new();
    private readonly Mock<IClaimsService> _claimsService = new();
    private readonly Mock<ICurrentTime> _currentTime = new();

    private VoucherService CreateSut(Guid? actorId = null)
    {
        _claimsService.Setup(c => c.GetCurrentUserId).Returns(actorId ?? _studentId);
        _currentTime.Setup(t => t.GetCurrentTime()).Returns(_now);
        return new VoucherService(
            _db,
            _claimsService.Object,
            _currentTime.Object,
            NullLogger<VoucherService>.Instance);
    }

    private void SeedStudent()
    {
        _db.Users.Seed(new User
        {
            Id = _studentId,
            Code = "STU-1",
            Email = "student@test.com",
            FullName = "Test Student",
            Role = RoleType.Student,
        });
    }

    private void SeedManager()
    {
        _db.Users.Seed(new User
        {
            Id = _managerId,
            Code = "MGR-1",
            Email = "manager@test.com",
            FullName = "Manager",
            Role = RoleType.Manager,
        });
    }

    private Voucher SeedVoucher(
        string code = "OBX15",
        decimal? percentOff = 15,
        decimal? amountOff = null,
        VoucherScope scope = VoucherScope.Both,
        DateTime? startsAt = null,
        DateTime? expiryAt = null,
        int? usageLimit = null,
        int? maxUsagePerStudent = null,
        VoucherStatus? status = null)
    {
        var voucher = new Voucher
        {
            Id = Guid.NewGuid(),
            Code = code,
            PercentOff = percentOff,
            AmountOff = amountOff,
            Scope = scope,
            StartsAt = startsAt,
            ExpiryAt = expiryAt,
            UsageLimit = usageLimit,
            MaxUsagePerStudent = maxUsagePerStudent,
            Status = status ?? (startsAt.HasValue && startsAt.Value > _now
                ? VoucherStatus.Draft
                : VoucherStatus.Active),
            CreatedAt = _now,
        };
        _db.Vouchers.Seed(voucher);
        return voucher;
    }

    private void SeedActiveBundleWithPrograms()
    {
        _db.Programs.Seed(
            new Program
            {
                Id = _programAId,
                Code = "PRG-A",
                Name = "Robotics 1",
                Category = ProgramCategory.Technology,
                Status = ProgramStatus.Active,
                Price = 1_000_000m,
            },
            new Program
            {
                Id = _programBId,
                Code = "PRG-B",
                Name = "Robotics 2",
                Category = ProgramCategory.Technology,
                Status = ProgramStatus.Active,
                Price = 2_000_000m,
            });

        _db.ProgramBundles.Seed(new ProgramBundle
        {
            Id = _bundleId,
            Code = "BDL-ROB",
            Name = "Robotics pathway",
            Category = ProgramCategory.Technology,
            Price = 2_500_000m,
            Status = ProgramBundleStatus.Active,
        });

        _db.ProgramBundleItems.Seed(
            new ProgramBundleItem
            {
                Id = Guid.NewGuid(),
                BundleId = _bundleId,
                ProgramId = _programAId,
                SortOrder = 1,
            },
            new ProgramBundleItem
            {
                Id = Guid.NewGuid(),
                BundleId = _bundleId,
                ProgramId = _programBId,
                SortOrder = 2,
            });
    }

    [Fact]
    public async Task CreateVoucher_NormalizesCodeAndPersists()
    {
        SeedManager();
        var sut = CreateSut(_managerId);

        var result = await sut.CreateVoucher(new CreateVoucherRequestDto
        {
            Code = " obx15 ",
            PercentOff = 15,
            Scope = VoucherScope.Both,
        });

        Assert.Equal("OBX15", result.Code);
        Assert.Equal(15m, result.PercentOff);
        Assert.Null(result.AmountOff);
        Assert.Null(result.StartsAt);
        Assert.False(result.IsNotYetActive);
        Assert.Equal(VoucherStatus.Active, result.Status);
        Assert.Equal(VoucherStatus.Active, _db.Vouchers.Items.Single().Status);
        Assert.Single(_db.Vouchers.Items);
    }

    [Fact]
    public async Task CreateVoucher_PersistsStartsAt()
    {
        SeedManager();
        var sut = CreateSut(_managerId);
        var startsAt = _now.AddDays(1);

        var result = await sut.CreateVoucher(new CreateVoucherRequestDto
        {
            Code = "FUTURE",
            PercentOff = 10,
            Scope = VoucherScope.Both,
            StartsAt = startsAt,
            ExpiryAt = _now.AddDays(7),
        });

        Assert.Equal(startsAt, result.StartsAt);
        Assert.True(result.IsNotYetActive);
        Assert.Equal(VoucherStatus.Draft, result.Status);
        Assert.Equal(VoucherStatus.Draft, _db.Vouchers.Items.Single().Status);
        Assert.Equal(startsAt, _db.Vouchers.Items.Single().StartsAt);
    }

    [Fact]
    public async Task CreateVoucher_StartsAtOnOrAfterExpiryAt_ThrowsBadRequest()
    {
        SeedManager();
        var sut = CreateSut(_managerId);

        await Assert.ThrowsAsync<BadRequestException>(() => sut.CreateVoucher(new CreateVoucherRequestDto
        {
            Code = "WINDOW",
            PercentOff = 10,
            Scope = VoucherScope.Both,
            StartsAt = _now.AddDays(3),
            ExpiryAt = _now.AddDays(3),
        }));
    }

    [Fact]
    public async Task CreateVoucher_BothDiscountTypes_ThrowsBadRequest()
    {
        SeedManager();
        var sut = CreateSut(_managerId);

        await Assert.ThrowsAsync<BadRequestException>(() => sut.CreateVoucher(new CreateVoucherRequestDto
        {
            Code = "BAD",
            PercentOff = 15,
            AmountOff = 1000,
            Scope = VoucherScope.Both,
        }));
    }

    [Fact]
    public async Task CreateVoucher_DuplicateCode_ThrowsConflict()
    {
        SeedManager();
        SeedVoucher();
        var sut = CreateSut(_managerId);

        await Assert.ThrowsAsync<ConflictException>(() => sut.CreateVoucher(new CreateVoucherRequestDto
        {
            Code = "OBX15",
            PercentOff = 10,
            Scope = VoucherScope.Program,
        }));
    }

    [Fact]
    public async Task PreviewVoucher_AppliesPercentAfterOwnershipDeduction()
    {
        SeedStudent();
        SeedActiveBundleWithPrograms();
        SeedVoucher();
        _db.ProgramEnrollments.Seed(new ProgramEnrollment
        {
            Id = Guid.NewGuid(),
            StudentId = _studentId,
            ProgramId = _programAId,
            Status = EnrollmentStatus.Active,
        });

        var sut = CreateSut(_studentId);
        var result = await sut.PreviewVoucher(_studentId, new PreviewVoucherRequestDto
        {
            Code = "obx15",
            BundleId = _bundleId,
        });

        Assert.True(result.IsValid);
        Assert.Equal(1_500_000m, result.BaseAmount);
        Assert.Equal(225_000m, result.DiscountAmount);
        Assert.Equal(1_275_000m, result.FinalAmount);
    }

    [Fact]
    public async Task PreviewVoucher_ZeroAfterOwnership_StillValidWithZeroDiscount()
    {
        SeedStudent();
        SeedActiveBundleWithPrograms();
        SeedVoucher();
        _db.ProgramEnrollments.Seed(
            new ProgramEnrollment
            {
                Id = Guid.NewGuid(),
                StudentId = _studentId,
                ProgramId = _programAId,
                Status = EnrollmentStatus.Completed,
            },
            new ProgramEnrollment
            {
                Id = Guid.NewGuid(),
                StudentId = _studentId,
                ProgramId = _programBId,
                Status = EnrollmentStatus.Active,
            });

        var sut = CreateSut(_studentId);
        var result = await sut.PreviewVoucher(_studentId, new PreviewVoucherRequestDto
        {
            Code = "OBX15",
            BundleId = _bundleId,
        });

        Assert.True(result.IsValid);
        Assert.Equal(0m, result.BaseAmount);
        Assert.Equal(0m, result.DiscountAmount);
        Assert.Equal(0m, result.FinalAmount);
    }

    [Fact]
    public async Task PreviewVoucher_Expired_ReturnsInvalidWithoutThrowing()
    {
        SeedStudent();
        SeedActiveBundleWithPrograms();
        SeedVoucher(expiryAt: _now.AddMinutes(-1));

        var sut = CreateSut(_studentId);
        var result = await sut.PreviewVoucher(_studentId, new PreviewVoucherRequestDto
        {
            Code = "OBX15",
            BundleId = _bundleId,
        });

        Assert.False(result.IsValid);
        Assert.Equal("Expired", result.ErrorCode);
        Assert.Equal(2_500_000m, result.BaseAmount);
        Assert.Equal(2_500_000m, result.FinalAmount);
    }

    [Fact]
    public async Task PreviewVoucher_NotYetActive_ReturnsInvalidWithoutThrowing()
    {
        SeedStudent();
        SeedActiveBundleWithPrograms();
        SeedVoucher(startsAt: _now.AddMinutes(1));

        var sut = CreateSut(_studentId);
        var result = await sut.PreviewVoucher(_studentId, new PreviewVoucherRequestDto
        {
            Code = "OBX15",
            BundleId = _bundleId,
        });

        Assert.False(result.IsValid);
        Assert.Equal("NotYetActive", result.ErrorCode);
        Assert.Equal(2_500_000m, result.BaseAmount);
        Assert.Equal(2_500_000m, result.FinalAmount);
    }

    [Fact]
    public async Task GetVoucherById_DueDraft_BecomesActive()
    {
        SeedManager();
        var voucher = SeedVoucher(startsAt: _now.AddMinutes(-1), status: VoucherStatus.Draft);
        var sut = CreateSut(_managerId);

        var result = await sut.GetVoucherById(voucher.Id);

        Assert.Equal(VoucherStatus.Active, result.Status);
        Assert.False(result.IsNotYetActive);
        Assert.Equal(VoucherStatus.Active, _db.Vouchers.Items.Single().Status);
    }

    [Fact]
    public async Task GetAllVouchers_FiltersByStatus()
    {
        SeedManager();
        SeedVoucher(code: "NOW");
        SeedVoucher(code: "LATER", startsAt: _now.AddDays(1));
        var sut = CreateSut(_managerId);

        var drafts = await sut.GetAllVouchers(null, null, VoucherStatus.Draft, 1, 10);
        var actives = await sut.GetAllVouchers(null, null, VoucherStatus.Active, 1, 10);

        Assert.Equal("LATER", Assert.Single(drafts.Items).Code);
        Assert.Equal("NOW", Assert.Single(actives.Items).Code);
    }

    [Fact]
    public async Task PreviewVoucher_DueDraft_ActivatesAndApplies()
    {
        SeedStudent();
        SeedActiveBundleWithPrograms();
        SeedVoucher(startsAt: _now.AddMinutes(-1), status: VoucherStatus.Draft);

        var sut = CreateSut(_studentId);
        var result = await sut.PreviewVoucher(_studentId, new PreviewVoucherRequestDto
        {
            Code = "OBX15",
            BundleId = _bundleId,
        });

        Assert.True(result.IsValid);
        Assert.Equal(VoucherStatus.Active, _db.Vouchers.Items.Single().Status);
    }

    [Fact]
    public async Task ValidateForCheckout_NotYetActive_ThrowsBadRequest()
    {
        SeedStudent();
        SeedActiveBundleWithPrograms();
        SeedVoucher(startsAt: _now.AddMinutes(1));

        var sut = CreateSut(_studentId);
        await Assert.ThrowsAsync<BadRequestException>(() => sut.ValidateForCheckout(
            _studentId,
            new PreviewVoucherRequestDto { Code = "OBX15", BundleId = _bundleId }));
    }

    [Fact]
    public async Task UpdateVoucher_StartsAtAfterExistingExpiry_ThrowsBadRequest()
    {
        SeedManager();
        var voucher = SeedVoucher(expiryAt: _now.AddDays(2));
        var sut = CreateSut(_managerId);

        await Assert.ThrowsAsync<BadRequestException>(() => sut.UpdateVoucher(
            voucher.Id,
            new UpdateVoucherRequestDto { StartsAt = _now.AddDays(3) }));
    }

    [Fact]
    public async Task ValidateForCheckout_Expired_ThrowsBadRequest()
    {
        SeedStudent();
        SeedActiveBundleWithPrograms();
        SeedVoucher(expiryAt: _now.AddMinutes(-1));

        var sut = CreateSut(_studentId);
        await Assert.ThrowsAsync<BadRequestException>(() => sut.ValidateForCheckout(
            _studentId,
            new PreviewVoucherRequestDto { Code = "OBX15", BundleId = _bundleId }));
    }

    [Fact]
    public async Task PreviewVoucher_ProgramScopeOnBundle_ReturnsScopeMismatch()
    {
        SeedStudent();
        SeedActiveBundleWithPrograms();
        SeedVoucher(scope: VoucherScope.Program);

        var sut = CreateSut(_studentId);
        var result = await sut.PreviewVoucher(_studentId, new PreviewVoucherRequestDto
        {
            Code = "OBX15",
            BundleId = _bundleId,
        });

        Assert.False(result.IsValid);
        Assert.Equal("ScopeMismatch", result.ErrorCode);
    }

    [Fact]
    public async Task PreviewVoucher_AmountOffClampsToBaseAmount()
    {
        SeedStudent();
        _db.Programs.Seed(new Program
        {
            Id = _programAId,
            Code = "PRG-A",
            Name = "Robotics 1",
            Category = ProgramCategory.Technology,
            Status = ProgramStatus.Active,
            Price = 100_000m,
        });
        SeedVoucher(percentOff: null, amountOff: 500_000m, scope: VoucherScope.Program);

        var sut = CreateSut(_studentId);
        var result = await sut.PreviewVoucher(_studentId, new PreviewVoucherRequestDto
        {
            Code = "OBX15",
            ProgramId = _programAId,
        });

        Assert.True(result.IsValid);
        Assert.Equal(100_000m, result.BaseAmount);
        Assert.Equal(100_000m, result.DiscountAmount);
        Assert.Equal(0m, result.FinalAmount);
    }

    [Fact]
    public async Task GetVoucherById_IncludesSuccessfulPaymentUsages()
    {
        SeedManager();
        SeedStudent();
        var voucher = SeedVoucher();
        _db.Payments.Seed(new Payment
        {
            Id = Guid.NewGuid(),
            Code = "INV-1",
            StudentId = _studentId,
            PaidById = _studentId,
            VoucherId = voucher.Id,
            Amount = 1_275_000m,
            DiscountAmount = 1_225_000m,
            Status = PaymentStatus.Success,
            PaidAt = _now,
            Currency = "VND",
        });
        _db.Payments.Seed(new Payment
        {
            Id = Guid.NewGuid(),
            Code = "INV-PENDING",
            StudentId = _studentId,
            PaidById = _studentId,
            VoucherId = voucher.Id,
            Amount = 1_275_000m,
            Status = PaymentStatus.Pending,
        });

        var sut = CreateSut(_managerId);
        var result = await sut.GetVoucherById(voucher.Id);

        Assert.Equal(1, result.UsageCount);
        var usage = Assert.Single(result.Usages);
        Assert.Equal("INV-1", usage.PaymentCode);
        Assert.Equal("Test Student", usage.StudentName);
    }

    [Fact]
    public async Task DeleteVoucher_SoftDeletes()
    {
        SeedManager();
        var voucher = SeedVoucher();
        var sut = CreateSut(_managerId);

        var deleted = await sut.DeleteVoucher(voucher.Id);

        Assert.True(deleted);
        Assert.True(_db.Vouchers.Items.Single().IsDeleted);
    }

    [Fact]
    public async Task PreviewVoucher_ParentForUnlinkedStudent_ThrowsForbidden()
    {
        SeedStudent();
        SeedActiveBundleWithPrograms();
        SeedVoucher();
        _db.Users.Seed(new User
        {
            Id = _parentId,
            Code = "PAR-1",
            Email = "parent@test.com",
            Role = RoleType.Parent,
        });

        var sut = CreateSut(_parentId);
        await Assert.ThrowsAsync<ForbiddenException>(() => sut.PreviewVoucher(
            _studentId,
            new PreviewVoucherRequestDto { Code = "OBX15", BundleId = _bundleId }));
    }
}
