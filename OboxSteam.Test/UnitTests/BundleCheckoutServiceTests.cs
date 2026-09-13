using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OboxSteam.Application.DTOs.EmailDTO;
using OboxSteam.Application.Exceptions;
using OboxSteam.Application.Interfaces;
using OboxSteam.Application.Notifications;
using OboxSteam.Application.Services;
using OboxSteam.Application.Validation;
using OboxSteam.Domain.Entities;
using OboxSteam.Domain.Enums;
using OboxSteam.Test.Helpers;

namespace OboxSteam.Test.UnitTests;

public sealed class BundleCheckoutServiceTests
{
    private readonly Guid _studentId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private readonly Guid _parentId = Guid.Parse("14141414-1414-1414-1414-141414141414");
    private readonly Guid _programAId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private readonly Guid _programBId = Guid.Parse("23232323-2323-2323-2323-232323232323");
    private readonly Guid _programCId = Guid.Parse("24242424-2424-2424-2424-242424242424");
    private readonly Guid _bundleId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private const string CheckoutSessionId = "cs_bundle_test";

    private readonly InMemoryUnitOfWork _db = new();
    private readonly Mock<IClaimsService> _claimsService = new();
    private readonly Mock<IProgramEnrollmentService> _programEnrollmentService = new();
    private readonly Mock<IStripePaymentService> _stripe = new();
    private readonly Mock<IEmailService> _emailService = new();
    private readonly Mock<INotificationPublisher> _notificationPublisher = new();
    private readonly Mock<IClassService> _classService = new();
    private readonly Mock<ICurrentTime> _currentTime = new();
    private readonly FakeSyncEventPublisher _syncEventPublisher = new();

    private PaymentService CreateSut(Guid? currentUserId = null)
    {
        _claimsService.Setup(c => c.GetCurrentUserId).Returns(currentUserId ?? _studentId);
        _currentTime.Setup(t => t.GetCurrentTime()).Returns(DateTime.UtcNow);
        _stripe
            .Setup(s => s.CreateCheckoutSession(
                It.IsAny<Payment>(),
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<string>(),
                It.IsAny<string>()))
            .ReturnsAsync(("https://checkout.stripe.com/bundle", CheckoutSessionId));
        _stripe
            .Setup(s => s.ParseWebhookEvent(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(("checkout.session.completed", CheckoutSessionId, "txn_bundle"));
        _emailService
            .Setup(e => e.SendPaymentRequestToParentEmailAsync(It.IsAny<PaymentRequestEmailDto>()))
            .Returns(Task.CompletedTask);
        _emailService
            .Setup(e => e.SendPaymentInvoiceEmailAsync(It.IsAny<InvoiceEmailDto>()))
            .Returns(Task.CompletedTask);
        _notificationPublisher
            .Setup(n => n.PublishAsync(It.IsAny<NotificationCommand>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _notificationPublisher
            .Setup(n => n.PublishManyAsync(It.IsAny<IReadOnlyList<NotificationCommand>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _classService
            .Setup(c => c.TryAutoStartClassIfReadyAsync(It.IsAny<Guid>()))
            .Returns(Task.CompletedTask);

        var lifecycle = new ProgramPurchaseLifecycle(
            _db,
            _currentTime.Object,
            _notificationPublisher.Object,
            NullLogger<ProgramPurchaseLifecycle>.Instance);
        var rebuyCatalog = new RebuyClassCatalogService(
            _db,
            _claimsService.Object,
            lifecycle,
            new ClassContinuityCatalogBuilder(_db),
            _currentTime.Object,
            NullLogger<RebuyClassCatalogService>.Instance);

        return new PaymentService(
            _db,
            _claimsService.Object,
            _programEnrollmentService.Object,
            _stripe.Object,
            _emailService.Object,
            new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["JWT:SecretKey"] = "this-is-a-test-secret-key-32chars!",
                    ["JWT:Issuer"] = "test",
                    ["JWT:Audience"] = "test",
                    ["APP_FRONTEND_URL"] = "https://app.test.com",
                })
                .Build(),
            NullLogger<PaymentService>.Instance,
            _notificationPublisher.Object,
            new ClassRedeliveryRequestService(
                _db,
                _claimsService.Object,
                _notificationPublisher.Object,
                rebuyCatalog,
                _currentTime.Object,
                NullLogger<ClassRedeliveryRequestService>.Instance),
            new ClassSeatHoldService(
                _db,
                _claimsService.Object,
                _programEnrollmentService.Object,
                _syncEventPublisher,
                _classService.Object,
                lifecycle,
                NullLogger<ClassSeatHoldService>.Instance),
            lifecycle,
            new VoucherService(
                _db,
                _claimsService.Object,
                _currentTime.Object,
                NullLogger<VoucherService>.Instance));
    }

    private void SeedStudent()
    {
        _db.Users.Seed(new User
        {
            Id = _studentId,
            Code = "STD-001",
            Email = "student@test.com",
            FullName = "Student One",
            Role = RoleType.Student,
        });
    }

    private void SeedParent()
    {
        _db.Users.Seed(new User
        {
            Id = _parentId,
            Code = "PRT-001",
            Email = "parent@test.com",
            FullName = "Parent One",
            Role = RoleType.Parent,
        });
        _db.ParentStudents.Seed(new ParentStudent
        {
            Id = Guid.NewGuid(),
            ParentId = _parentId,
            StudentId = _studentId,
            IsVerified = true,
        });
    }

    private void SeedRoboticsBundle()
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
                Price = 1_500_000m,
            },
            new Program
            {
                Id = _programCId,
                Code = "PRG-C",
                Name = "Robotics 3",
                Category = ProgramCategory.Technology,
                Status = ProgramStatus.Active,
                Price = 1_800_000m,
            });

        _db.ProgramBundles.Seed(new ProgramBundle
        {
            Id = _bundleId,
            Code = "BDL-ROB",
            Name = "Robotics pathway",
            Category = ProgramCategory.Technology,
            Price = 3_000_000m,
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
                RequiresPreviousCompletion = true,
            },
            new ProgramBundleItem
            {
                Id = Guid.NewGuid(),
                BundleId = _bundleId,
                ProgramId = _programCId,
                SortOrder = 3,
                RequiresPreviousCompletion = true,
            });
    }

    private void SeedCompletedProgramA()
    {
        _db.ProgramEnrollments.Seed(new ProgramEnrollment
        {
            Id = Guid.NewGuid(),
            StudentId = _studentId,
            ProgramId = _programAId,
            Status = EnrollmentStatus.Completed,
            ProgressPercent = 100m,
        });
    }

    [Fact]
    public async Task CreateBundleCheckout_NoOwnedPrograms_ChargesBundlePrice()
    {
        SeedStudent();
        SeedRoboticsBundle();
        var sut = CreateSut();

        var result = await sut.CreateBundleCheckout(_bundleId, PaymentGateway.Stripe);

        var payment = Assert.Single(_db.Payments.Items);
        Assert.Equal(3_000_000m, payment.Amount);
        Assert.Equal(0m, payment.DiscountAmount);
        Assert.False(result.Activated);
        Assert.Equal("https://checkout.stripe.com/bundle", result.CheckoutUrl);
        Assert.Equal(BundleEnrollmentStatus.PendingPayment, _db.BundleEnrollments.Items.Single().Status);
        Assert.Empty(_db.ProgramEnrollments.Items);
    }

    [Fact]
    public async Task CreateBundleCheckout_DeductsCompletedProgramRetail()
    {
        SeedStudent();
        SeedRoboticsBundle();
        SeedCompletedProgramA();
        var sut = CreateSut();

        var result = await sut.CreateBundleCheckout(_bundleId, PaymentGateway.Stripe);

        var payment = Assert.Single(_db.Payments.Items);
        Assert.Equal(2_000_000m, payment.Amount);
        Assert.Equal(1_000_000m, payment.DiscountAmount);
        Assert.False(result.Activated);
        Assert.Equal(2_000_000m, result.Amount);
        Assert.Equal(1_000_000m, result.DiscountAmount);
    }

    [Fact]
    public async Task CreateBundleCheckout_AppliesVoucherAfterOwnership()
    {
        SeedStudent();
        SeedRoboticsBundle();
        SeedCompletedProgramA();
        _db.Vouchers.Seed(new Voucher
        {
            Id = Guid.NewGuid(),
            Code = "OBX15",
            PercentOff = 15m,
            Scope = VoucherScope.Bundle,
            Status = VoucherStatus.Active,
        });
        var sut = CreateSut();

        await sut.CreateBundleCheckout(_bundleId, PaymentGateway.Stripe, "obx15");

        var payment = Assert.Single(_db.Payments.Items);
        Assert.Equal(1_700_000m, payment.Amount);
        Assert.Equal(1_300_000m, payment.DiscountAmount);
        Assert.NotNull(payment.VoucherId);
    }

    [Fact]
    public async Task CreateBundleCheckout_OwnershipCoversPrice_ActivatesWithoutStripe()
    {
        SeedStudent();
        SeedRoboticsBundle();
        _db.ProgramEnrollments.Seed(
            new ProgramEnrollment
            {
                Id = Guid.NewGuid(),
                StudentId = _studentId,
                ProgramId = _programAId,
                Status = EnrollmentStatus.Completed,
                ProgressPercent = 100m,
            },
            new ProgramEnrollment
            {
                Id = Guid.NewGuid(),
                StudentId = _studentId,
                ProgramId = _programBId,
                Status = EnrollmentStatus.Completed,
                ProgressPercent = 100m,
            },
            new ProgramEnrollment
            {
                Id = Guid.NewGuid(),
                StudentId = _studentId,
                ProgramId = _programCId,
                Status = EnrollmentStatus.Completed,
                ProgressPercent = 100m,
            });
        var sut = CreateSut();

        var result = await sut.CreateBundleCheckout(_bundleId, PaymentGateway.Stripe);

        Assert.True(result.Activated);
        Assert.Equal(string.Empty, result.CheckoutUrl);
        var payment = Assert.Single(_db.Payments.Items);
        Assert.Equal(0m, payment.Amount);
        Assert.Equal(3_000_000m, payment.DiscountAmount);
        Assert.Equal(PaymentStatus.Success, payment.Status);
        Assert.Equal(BundleEnrollmentStatus.Active, _db.BundleEnrollments.Items.Single().Status);
        Assert.Equal(3, _db.ProgramEnrollments.Items.Count);
        _stripe.Verify(
            s => s.CreateCheckoutSession(
                It.IsAny<Payment>(),
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<string>(),
                It.IsAny<string>()),
            Times.Never);
    }

    [Fact]
    public async Task HandleStripeWebhook_Bundle_CreatesActivePesForUnownedIncludingGated()
    {
        SeedStudent();
        SeedRoboticsBundle();
        SeedCompletedProgramA();
        var bundleEnrollment = new BundleEnrollment
        {
            Id = Guid.NewGuid(),
            StudentId = _studentId,
            BundleId = _bundleId,
            Status = BundleEnrollmentStatus.PendingPayment,
        };
        _db.BundleEnrollments.Seed(bundleEnrollment);
        _db.Payments.Seed(new Payment
        {
            Id = Guid.NewGuid(),
            Code = "PAY-BDL",
            StudentId = _studentId,
            PaidById = _studentId,
            BundleEnrollmentId = bundleEnrollment.Id,
            Amount = 2_000_000m,
            DiscountAmount = 1_000_000m,
            Gateway = PaymentGateway.Stripe,
            Status = PaymentStatus.Pending,
            CheckoutSessionId = CheckoutSessionId,
        });
        var sut = CreateSut();

        await sut.HandleStripeWebhook("{}", "sig");

        Assert.Equal(BundleEnrollmentStatus.Active, _db.BundleEnrollments.Items.Single().Status);
        Assert.Equal(PaymentStatus.Success, _db.Payments.Items.Single().Status);
        Assert.Contains(_db.ProgramEnrollments.Items, pe => pe.ProgramId == _programAId && pe.Status == EnrollmentStatus.Completed);
        Assert.Contains(_db.ProgramEnrollments.Items, pe => pe.ProgramId == _programBId && pe.Status == EnrollmentStatus.Active);
        Assert.Contains(_db.ProgramEnrollments.Items, pe => pe.ProgramId == _programCId && pe.Status == EnrollmentStatus.Active);
        Assert.Equal(3, _db.ProgramEnrollments.Items.Count);
        var invoice = Assert.Single(_db.Invoices.Items);
        Assert.Equal(3_000_000m, invoice.SubTotal);
        Assert.Equal(2_000_000m, invoice.TotalAmount);
    }

    [Fact]
    public async Task CreateBundleCheckout_Conflict_WhenAlreadyActive()
    {
        SeedStudent();
        SeedRoboticsBundle();
        _db.BundleEnrollments.Seed(new BundleEnrollment
        {
            Id = Guid.NewGuid(),
            StudentId = _studentId,
            BundleId = _bundleId,
            Status = BundleEnrollmentStatus.Active,
        });
        var sut = CreateSut();

        await Assert.ThrowsAsync<ConflictException>(() =>
            sut.CreateBundleCheckout(_bundleId, PaymentGateway.Stripe));
    }

    [Fact]
    public async Task CreateBundleCheckout_ProgramScopeVoucher_Throws()
    {
        SeedStudent();
        SeedRoboticsBundle();
        _db.Vouchers.Seed(new Voucher
        {
            Id = Guid.NewGuid(),
            Code = "PRGONLY",
            PercentOff = 10m,
            Scope = VoucherScope.Program,
            Status = VoucherStatus.Active,
        });
        var sut = CreateSut();

        await Assert.ThrowsAsync<BadRequestException>(() =>
            sut.CreateBundleCheckout(_bundleId, PaymentGateway.Stripe, "PRGONLY"));
    }

    [Fact]
    public async Task RequestParentBundlePayment_ChargesOwnershipPriceWithoutVoucher()
    {
        SeedStudent();
        SeedParent();
        SeedRoboticsBundle();
        SeedCompletedProgramA();
        _db.Vouchers.Seed(new Voucher
        {
            Id = Guid.NewGuid(),
            Code = "OBX15",
            PercentOff = 15m,
            Scope = VoucherScope.Both,
            Status = VoucherStatus.Active,
        });
        var sut = CreateSut();

        await sut.RequestParentBundlePayment(_bundleId, _parentId);

        var request = Assert.Single(_db.PaymentRequests.Items);
        Assert.Equal(2_000_000m, request.Amount);
        Assert.Equal(_db.BundleEnrollments.Items.Single().Id, request.BundleEnrollmentId);
        Assert.Empty(_db.Payments.Items);
        _emailService.Verify(
            e => e.SendPaymentRequestToParentEmailAsync(It.Is<PaymentRequestEmailDto>(d => d.Amount == 2_000_000m)),
            Times.Once);
    }

    [Fact]
    public async Task CreateParentCheckout_Bundle_UsesFrozenAmount()
    {
        SeedStudent();
        SeedParent();
        SeedRoboticsBundle();
        var bundleEnrollment = new BundleEnrollment
        {
            Id = Guid.NewGuid(),
            StudentId = _studentId,
            BundleId = _bundleId,
            Status = BundleEnrollmentStatus.PendingPayment,
        };
        _db.BundleEnrollments.Seed(bundleEnrollment);
        _db.PaymentRequests.Seed(new PaymentRequest
        {
            Id = Guid.NewGuid(),
            StudentId = _studentId,
            ParentId = _parentId,
            BundleEnrollmentId = bundleEnrollment.Id,
            Amount = 2_000_000m,
            Currency = "VND",
            Token = "bundletoken",
            ExpiresAt = DateTime.UtcNow.AddHours(12),
            Status = PaymentRequestStatus.Pending,
        });
        var sut = CreateSut(_parentId);

        var result = await sut.CreateParentCheckout("bundletoken", PaymentGateway.Stripe);

        var payment = Assert.Single(_db.Payments.Items);
        Assert.Equal(2_000_000m, payment.Amount);
        Assert.Equal(1_000_000m, payment.DiscountAmount);
        Assert.Null(payment.VoucherId);
        Assert.Equal(bundleEnrollment.Id, payment.BundleEnrollmentId);
        Assert.False(result.Activated);
        Assert.Equal("https://checkout.stripe.com/bundle", result.CheckoutUrl);
    }

    [Fact]
    public async Task GatedBundleItem_BlocksClassEnroll_UntilPreviousCompleted()
    {
        SeedStudent();
        SeedRoboticsBundle();
        var peB = new ProgramEnrollment
        {
            Id = Guid.NewGuid(),
            StudentId = _studentId,
            ProgramId = _programBId,
            Status = EnrollmentStatus.Active,
        };
        _db.ProgramEnrollments.Seed(
            new ProgramEnrollment
            {
                Id = Guid.NewGuid(),
                StudentId = _studentId,
                ProgramId = _programAId,
                Status = EnrollmentStatus.Active,
                ProgressPercent = 40m,
            },
            peB);
        _db.BundleEnrollments.Seed(new BundleEnrollment
        {
            Id = Guid.NewGuid(),
            StudentId = _studentId,
            BundleId = _bundleId,
            Status = BundleEnrollmentStatus.Active,
        });

        await Assert.ThrowsAsync<BadRequestException>(() =>
            BundleEnrollmentHelper.ValidateBundlePrerequisiteAsync(_db, _studentId, _programBId));
    }
}
