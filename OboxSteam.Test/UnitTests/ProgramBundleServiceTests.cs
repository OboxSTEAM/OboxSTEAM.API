using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OboxSteam.Application.DTOs.ProgramBundleDTO;
using OboxSteam.Application.Exceptions;
using OboxSteam.Application.Interfaces;
using OboxSteam.Application.Services;
using OboxSteam.Domain.Entities;
using OboxSteam.Domain.Enums;
using OboxSteam.Test.Helpers;

namespace OboxSteam.Test.UnitTests;

public sealed class ProgramBundleServiceTests
{
    private readonly Guid _managerId = Guid.Parse("13131313-1313-1313-1313-131313131313");
    private readonly Guid _studentId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private readonly Guid _parentId = Guid.Parse("14141414-1414-1414-1414-141414141414");
    private readonly Guid _programAId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private readonly Guid _programBId = Guid.Parse("23232323-2323-2323-2323-232323232323");
    private readonly Guid _bundleId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private readonly DateTime _now = new(2026, 9, 10, 0, 0, 0, DateTimeKind.Utc);

    private readonly InMemoryUnitOfWork _db = new();
    private readonly Mock<IClaimsService> _claimsService = new();
    private readonly Mock<ICurrentTime> _currentTime = new();

    private ProgramBundleService CreateSut(Guid? actorId = null)
    {
        _claimsService.Setup(c => c.GetCurrentUserId).Returns(actorId ?? _studentId);
        _currentTime.Setup(t => t.GetCurrentTime()).Returns(_now);
        var voucherService = new VoucherService(
            _db,
            _claimsService.Object,
            _currentTime.Object,
            NullLogger<VoucherService>.Instance);
        return new ProgramBundleService(
            _db,
            _claimsService.Object,
            voucherService,
            NullLogger<ProgramBundleService>.Instance);
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

    private void SeedVoucher(string code = "OBX15", decimal percentOff = 15)
    {
        _db.Vouchers.Seed(new Voucher
        {
            Id = Guid.NewGuid(),
            Code = code,
            PercentOff = percentOff,
            Scope = VoucherScope.Both,
            Status = VoucherStatus.Active,
            CreatedAt = _now,
        });
    }

    [Fact]
    public async Task GetPriceQuote_NoOwnedPrograms_ReturnsBundlePrice()
    {
        SeedStudent();
        SeedActiveBundleWithPrograms();
        var sut = CreateSut();

        var result = await sut.GetPriceQuote(_bundleId, _studentId, null);

        Assert.Equal(_bundleId, result.BundleId);
        Assert.Equal("Robotics pathway", result.BundleName);
        Assert.Equal(2_500_000m, result.BundlePrice);
        Assert.Empty(result.OwnedPrograms);
        Assert.Equal(0m, result.OwnershipDeduction);
        Assert.Equal(2_500_000m, result.PriceAfterOwnership);
        Assert.Null(result.Voucher);
        Assert.Equal(2_500_000m, result.FinalPrice);
    }

    [Fact]
    public async Task GetPriceQuote_DeductsOwnedActiveProgramRetail()
    {
        SeedStudent();
        SeedActiveBundleWithPrograms();
        _db.ProgramEnrollments.Seed(new ProgramEnrollment
        {
            Id = Guid.NewGuid(),
            StudentId = _studentId,
            ProgramId = _programAId,
            Status = EnrollmentStatus.Active,
        });
        var sut = CreateSut();

        var result = await sut.GetPriceQuote(_bundleId, _studentId, null);

        var owned = Assert.Single(result.OwnedPrograms);
        Assert.Equal(_programAId, owned.ProgramId);
        Assert.Equal("Robotics 1", owned.Name);
        Assert.Equal(1_000_000m, owned.DeductedPrice);
        Assert.Equal(1_000_000m, result.OwnershipDeduction);
        Assert.Equal(1_500_000m, result.PriceAfterOwnership);
        Assert.Equal(1_500_000m, result.FinalPrice);
    }

    [Fact]
    public async Task GetPriceQuote_IgnoresPendingPaymentEnrollment()
    {
        SeedStudent();
        SeedActiveBundleWithPrograms();
        _db.ProgramEnrollments.Seed(new ProgramEnrollment
        {
            Id = Guid.NewGuid(),
            StudentId = _studentId,
            ProgramId = _programAId,
            Status = EnrollmentStatus.PendingPayment,
        });
        var sut = CreateSut();

        var result = await sut.GetPriceQuote(_bundleId, _studentId, null);

        Assert.Empty(result.OwnedPrograms);
        Assert.Equal(2_500_000m, result.FinalPrice);
    }

    [Fact]
    public async Task GetPriceQuote_ClampsToZeroWhenOwnedRetailExceedsBundlePrice()
    {
        SeedStudent();
        SeedActiveBundleWithPrograms();
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
        var sut = CreateSut();

        var result = await sut.GetPriceQuote(_bundleId, _studentId, null);

        Assert.Equal(2, result.OwnedPrograms.Count);
        Assert.Equal(3_000_000m, result.OwnershipDeduction);
        Assert.Equal(0m, result.PriceAfterOwnership);
        Assert.Equal(0m, result.FinalPrice);
    }

    [Fact]
    public async Task GetPriceQuote_AppliesVoucherAfterOwnership()
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
        var sut = CreateSut();

        var result = await sut.GetPriceQuote(_bundleId, _studentId, "obx15");

        Assert.Equal(1_500_000m, result.PriceAfterOwnership);
        Assert.NotNull(result.Voucher);
        Assert.True(result.Voucher.IsValid);
        Assert.Equal(225_000m, result.Voucher.DiscountAmount);
        Assert.Equal(1_275_000m, result.FinalPrice);
    }

    [Fact]
    public async Task GetPriceQuote_InvalidVoucher_KeepsOwnershipPrice()
    {
        SeedStudent();
        SeedActiveBundleWithPrograms();
        SeedVoucher();
        var sut = CreateSut();

        var result = await sut.GetPriceQuote(_bundleId, _studentId, "NOPE");

        Assert.Equal(2_500_000m, result.PriceAfterOwnership);
        Assert.NotNull(result.Voucher);
        Assert.False(result.Voucher.IsValid);
        Assert.Equal("NotFound", result.Voucher.ErrorCode);
        Assert.Equal(2_500_000m, result.FinalPrice);
    }

    [Fact]
    public async Task GetPriceQuote_MissingBundle_ThrowsNotFound()
    {
        SeedStudent();
        var sut = CreateSut();

        await Assert.ThrowsAsync<NotFoundException>(() =>
            sut.GetPriceQuote(_bundleId, _studentId, null));
    }

    [Fact]
    public async Task GetPriceQuote_InactiveBundle_ThrowsBadRequest()
    {
        SeedStudent();
        SeedActiveBundleWithPrograms();
        _db.ProgramBundles.Items.Single().Status = ProgramBundleStatus.Draft;
        var sut = CreateSut();

        await Assert.ThrowsAsync<BadRequestException>(() =>
            sut.GetPriceQuote(_bundleId, _studentId, null));
    }

    [Fact]
    public async Task GetPriceQuote_ParentForUnlinkedStudent_ThrowsForbidden()
    {
        SeedStudent();
        SeedActiveBundleWithPrograms();
        _db.Users.Seed(new User
        {
            Id = _parentId,
            Code = "PAR-1",
            Email = "parent@test.com",
            Role = RoleType.Parent,
        });
        var sut = CreateSut(_parentId);

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            sut.GetPriceQuote(_bundleId, _studentId, null));
    }

    [Fact]
    public async Task CreateBundle_PersistsDraftWithItems()
    {
        SeedManager();
        SeedActiveBundleWithPrograms();
        var sut = CreateSut(_managerId);

        var result = await sut.CreateBundle(new CreateProgramBundleRequestDto
        {
            Code = " bdl-new ",
            Name = "  New pathway  ",
            Category = ProgramCategory.Technology,
            Price = 2_200_000m,
            Items =
            [
                new CreateProgramBundleItemRequestDto { ProgramId = _programAId },
                new CreateProgramBundleItemRequestDto
                {
                    ProgramId = _programBId,
                    RequiresPreviousCompletion = true,
                },
            ],
        });

        Assert.Equal("BDL-NEW", result.Code);
        Assert.Equal("New pathway", result.Name);
        Assert.Equal(ProgramBundleStatus.Draft, result.Status);
        Assert.Equal(2_200_000m, result.Price);
        Assert.Equal(3_000_000m, result.RetailTotal);
        Assert.Equal(2, result.Items.Count);
        Assert.Equal(1, result.Items[0].SortOrder);
        Assert.False(result.Items[0].RequiresPreviousCompletion);
        Assert.True(result.Items[1].RequiresPreviousCompletion);
        Assert.Equal(ProgramBundleStatus.Draft, _db.ProgramBundles.Items.Single(b => b.Code == "BDL-NEW").Status);
    }

    [Fact]
    public async Task CreateBundle_DuplicateCode_ThrowsConflict()
    {
        SeedManager();
        SeedActiveBundleWithPrograms();
        var sut = CreateSut(_managerId);

        await Assert.ThrowsAsync<ConflictException>(() => sut.CreateBundle(new CreateProgramBundleRequestDto
        {
            Code = "BDL-ROB",
            Name = "Copy",
            Category = ProgramCategory.Technology,
            Price = 1m,
        }));
    }

    [Fact]
    public async Task CreateBundle_NegativePrice_ThrowsBadRequest()
    {
        SeedManager();
        var sut = CreateSut(_managerId);

        await Assert.ThrowsAsync<BadRequestException>(() => sut.CreateBundle(new CreateProgramBundleRequestDto
        {
            Code = "BDL-BAD",
            Name = "Bad",
            Category = ProgramCategory.Technology,
            Price = -1m,
        }));
    }

    [Fact]
    public async Task PublishBundle_ActivatesWhenPriceBelowRetail()
    {
        SeedManager();
        SeedActiveBundleWithPrograms();
        _db.ProgramBundles.Items.Single().Status = ProgramBundleStatus.Draft;
        var sut = CreateSut(_managerId);

        var result = await sut.PublishBundle(_bundleId);

        Assert.Equal(ProgramBundleStatus.Active, result.Status);
        Assert.Equal(ProgramBundleStatus.Active, _db.ProgramBundles.Items.Single().Status);
    }

    [Fact]
    public async Task PublishBundle_PriceNotDiscounted_ThrowsBadRequest()
    {
        SeedManager();
        SeedActiveBundleWithPrograms();
        _db.ProgramBundles.Items.Single().Status = ProgramBundleStatus.Draft;
        _db.ProgramBundles.Items.Single().Price = 3_000_000m;
        var sut = CreateSut(_managerId);

        await Assert.ThrowsAsync<BadRequestException>(() => sut.PublishBundle(_bundleId));
    }

    [Fact]
    public async Task PublishBundle_AlreadyActive_ThrowsConflict()
    {
        SeedManager();
        SeedActiveBundleWithPrograms();
        var sut = CreateSut(_managerId);

        await Assert.ThrowsAsync<ConflictException>(() => sut.PublishBundle(_bundleId));
    }

    [Fact]
    public async Task PublishBundle_FewerThanTwoItems_ThrowsBadRequest()
    {
        SeedManager();
        _db.ProgramBundles.Seed(new ProgramBundle
        {
            Id = _bundleId,
            Code = "BDL-ONE",
            Name = "One item",
            Category = ProgramCategory.Technology,
            Price = 1m,
            Status = ProgramBundleStatus.Draft,
        });
        var sut = CreateSut(_managerId);

        await Assert.ThrowsAsync<BadRequestException>(() => sut.PublishBundle(_bundleId));
    }

    [Fact]
    public async Task GetBundleById_IncludesOrderedItems()
    {
        SeedActiveBundleWithPrograms();
        var sut = CreateSut(Guid.Empty);

        var result = await sut.GetBundleById(_bundleId);

        Assert.Equal(2, result.Items.Count);
        Assert.Equal(3_000_000m, result.RetailTotal);
        Assert.Equal(_programAId, result.Items[0].ProgramId);
        Assert.Equal(_programBId, result.Items[1].ProgramId);
    }

    [Fact]
    public async Task GetAllBundles_PaginatesAndFiltersByStatus()
    {
        SeedManager();
        _db.ProgramBundles.Seed(
            new ProgramBundle
            {
                Id = Guid.NewGuid(),
                Code = "BDL-A",
                Name = "Alpha pathway",
                Category = ProgramCategory.Technology,
                Price = 1m,
                Status = ProgramBundleStatus.Active,
                CreatedAt = _now.AddDays(-2),
            },
            new ProgramBundle
            {
                Id = Guid.NewGuid(),
                Code = "BDL-B",
                Name = "Beta pathway",
                Category = ProgramCategory.Science,
                Price = 1m,
                Status = ProgramBundleStatus.Draft,
                CreatedAt = _now.AddDays(-1),
            },
            new ProgramBundle
            {
                Id = Guid.NewGuid(),
                Code = "BDL-C",
                Name = "Gamma pathway",
                Category = ProgramCategory.Technology,
                Price = 1m,
                Status = ProgramBundleStatus.Draft,
                CreatedAt = _now,
            });
        var sut = CreateSut(_managerId);

        var page1 = await sut.GetAllBundles(null, ProgramBundleStatus.Draft, null, 1, 1);
        var search = await sut.GetAllBundles("beta", null, null, 1, 10);
        var tech = await sut.GetAllBundles(null, null, ProgramCategory.Technology, 1, 10);

        Assert.Equal(2, page1.TotalCount);
        Assert.Equal(2, page1.TotalPages);
        Assert.Equal("BDL-C", Assert.Single(page1.Items).Code);
        Assert.Empty(page1.Items[0].Items);
        Assert.Equal("BDL-B", Assert.Single(search.Items).Code);
        Assert.Equal(2, tech.TotalCount);
    }

    [Fact]
    public async Task GetAllBundles_Student_ReturnsOnlyActive()
    {
        SeedStudent();
        SeedMixedStatusBundles();
        var sut = CreateSut(_studentId);

        var all = await sut.GetAllBundles(null, ProgramBundleStatus.Draft, null, 1, 10);

        var item = Assert.Single(all.Items);
        Assert.Equal("BDL-A", item.Code);
        Assert.Equal(ProgramBundleStatus.Active, item.Status);
        Assert.Empty(item.Items);
    }

    [Fact]
    public async Task GetAllBundles_Parent_ReturnsOnlyActive()
    {
        SeedParent();
        SeedMixedStatusBundles();
        var sut = CreateSut(_parentId);

        var all = await sut.GetAllBundles(null, null, null, 1, 10);

        Assert.Equal("BDL-A", Assert.Single(all.Items).Code);
    }

    [Fact]
    public async Task GetAllBundles_Anonymous_ReturnsOnlyActive()
    {
        SeedMixedStatusBundles();
        var sut = CreateSut(Guid.Empty);

        var all = await sut.GetAllBundles(null, null, null, 1, 10);

        Assert.Equal("BDL-A", Assert.Single(all.Items).Code);
    }

    private void SeedParent()
    {
        _db.Users.Seed(new User
        {
            Id = _parentId,
            Code = "PAR-1",
            Email = "parent@test.com",
            FullName = "Test Parent",
            Role = RoleType.Parent,
        });
    }

    private void SeedMixedStatusBundles()
    {
        _db.ProgramBundles.Seed(
            new ProgramBundle
            {
                Id = Guid.NewGuid(),
                Code = "BDL-A",
                Name = "Alpha pathway",
                Category = ProgramCategory.Technology,
                Price = 1m,
                Status = ProgramBundleStatus.Active,
                CreatedAt = _now.AddDays(-2),
            },
            new ProgramBundle
            {
                Id = Guid.NewGuid(),
                Code = "BDL-B",
                Name = "Beta pathway",
                Category = ProgramCategory.Science,
                Price = 1m,
                Status = ProgramBundleStatus.Draft,
                CreatedAt = _now.AddDays(-1),
            });
    }
}
