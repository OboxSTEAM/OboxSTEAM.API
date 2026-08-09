using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OboxSteam.Application.DTOs.EmailDTO;
using OboxSteam.Application.Exceptions;
using OboxSteam.Application.Interfaces;
using OboxSteam.Application.Notifications;
using OboxSteam.Application.Services;
using OboxSteam.Domain.Entities;
using OboxSteam.Domain.Enums;
using OboxSteam.Test.Helpers;

namespace OboxSteam.Test.UnitTests;

public sealed class PaymentServiceTests
{
    private readonly Guid _studentId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private readonly Guid _parentId = Guid.Parse("14141414-1414-1414-1414-141414141414");
    private readonly Guid _managerId = Guid.Parse("13131313-1313-1313-1313-131313131313");
    private readonly Guid _programId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private readonly Guid _enrollmentId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private readonly Guid _moduleId = Guid.Parse("44444444-4444-4444-4444-444444444444");
    private readonly Guid _moduleEnrollmentId = Guid.Parse("55555555-5555-5555-5555-555555555555");
    private readonly Guid _paymentId = Guid.Parse("66666666-6666-6666-6666-666666666666");
    private readonly Guid _paymentRequestId = Guid.Parse("77777777-7777-7777-7777-777777777777");
    private const string CheckoutSessionId = "cs_test_123";
    private const string RequestToken = "paytoken123";

    private readonly InMemoryUnitOfWork _db = new();
    private readonly Mock<IClaimsService> _claimsService = new();
    private readonly Mock<IProgramEnrollmentService> _programEnrollmentService = new();
    private readonly Mock<IStripePaymentService> _stripe = new();
    private readonly Mock<IEmailService> _emailService = new();
    private readonly Mock<INotificationPublisher> _notificationPublisher = new();

    private static IConfiguration CreateConfiguration() =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["JWT:SecretKey"] = "this-is-a-test-secret-key-32chars!",
                ["JWT:Issuer"] = "test",
                ["JWT:Audience"] = "test",
                ["APP_BASE_URL"] = "https://test.example.com",
                ["APP_FRONTEND_URL"] = "https://app.test.com",
            })
            .Build();

    private PaymentService CreateSut(Guid? currentUserId = null)
    {
        _claimsService.Setup(c => c.GetCurrentUserId).Returns(currentUserId ?? _studentId);
        _stripe
            .Setup(s => s.CreateCheckoutSession(
                It.IsAny<Payment>(),
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<string>(),
                It.IsAny<string>()))
            .ReturnsAsync(("https://checkout.stripe.com/test", CheckoutSessionId));
        _stripe
            .Setup(s => s.ParseWebhookEvent(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(("checkout.session.completed", CheckoutSessionId, "txn_123"));
        _emailService
            .Setup(e => e.SendPaymentRequestToParentEmailAsync(It.IsAny<PaymentRequestEmailDto>()))
            .Returns(Task.CompletedTask);
        _emailService
            .Setup(e => e.SendPaymentInvoiceEmailAsync(It.IsAny<InvoiceEmailDto>()))
            .Returns(Task.CompletedTask);
        _emailService
            .Setup(e => e.SendEnrollmentConfirmationEmailAsync(It.IsAny<EnrollmentConfirmationEmailDto>()))
            .Returns(Task.CompletedTask);
        _notificationPublisher
            .Setup(n => n.PublishAsync(It.IsAny<NotificationCommand>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _notificationPublisher
            .Setup(n => n.PublishManyAsync(It.IsAny<IReadOnlyList<NotificationCommand>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _programEnrollmentService
            .Setup(s => s.GetOrCreatePendingEnrollmentAsync(_studentId, _programId))
            .ReturnsAsync(new ProgramEnrollment
            {
                Id = _enrollmentId,
                StudentId = _studentId,
                ProgramId = _programId,
                Status = EnrollmentStatus.PendingPayment,
                IsDeleted = false,
            });

        return new PaymentService(
            _db,
            _claimsService.Object,
            _programEnrollmentService.Object,
            _stripe.Object,
            _emailService.Object,
            CreateConfiguration(),
            NullLogger<PaymentService>.Instance,
            _notificationPublisher.Object);
    }

    private User SeedStudent(Guid? id = null)
    {
        var user = new User
        {
            Id = id ?? _studentId,
            Code = "STD-001",
            Email = "student@test.com",
            FullName = "Student One",
            Role = RoleType.Student,
            IsDeleted = false,
        };
        _db.Users.Seed(user);
        return user;
    }

    private User SeedParent()
    {
        var user = new User
        {
            Id = _parentId,
            Code = "PRT-001",
            Email = "parent@test.com",
            FullName = "Parent One",
            Role = RoleType.Parent,
            IsDeleted = false,
        };
        _db.Users.Seed(user);
        return user;
    }

    private Program SeedProgram(decimal? price = 500_000m, Guid? id = null)
    {
        var programId = id ?? _programId;
        var program = new Program
        {
            Id = programId,
            Code = programId == _programId ? "PRG-001" : "PRG-002",
            Name = "Robotics",
            Category = ProgramCategory.Technology,
            Level = DifficultyLevel.Beginner,
            Price = price,
            Description = "<p>Learn robotics</p>",
            IsDeleted = false,
        };
        _db.Programs.Seed(program);
        return program;
    }

    private Module SeedModule(decimal retakeFee = 100_000m)
    {
        var module = new Module
        {
            Id = _moduleId,
            Code = "MOD-001",
            Name = "Module A",
            ProgramId = _programId,
            ModuleType = ModuleType.Theory,
            RetakeFee = retakeFee,
            IsDeleted = false,
        };
        _db.Modules.Seed(module);
        return module;
    }

    private ModuleEnrollment SeedModuleEnrollment(Module module, EnrollmentStatus status = EnrollmentStatus.PendingPayment)
    {
        var enrollment = new ModuleEnrollment
        {
            Id = _moduleEnrollmentId,
            StudentId = _studentId,
            ModuleId = module.Id,
            Module = module,
            Status = status,
            IsDeleted = false,
        };
        _db.ModuleEnrollments.Seed(enrollment);
        return enrollment;
    }

    private Payment SeedPendingPayment(
        Guid? id = null,
        Guid? programEnrollmentId = null,
        Guid? moduleEnrollmentId = null,
        Guid? paidById = null)
    {
        var payment = new Payment
        {
            Id = id ?? _paymentId,
            Code = "PAY-001",
            StudentId = _studentId,
            PaidById = paidById ?? _studentId,
            ProgramEnrollmentId = programEnrollmentId,
            ModuleEnrollmentId = moduleEnrollmentId,
            Amount = 500_000m,
            Currency = "VND",
            Gateway = PaymentGateway.Stripe,
            Status = PaymentStatus.Pending,
            CheckoutSessionId = CheckoutSessionId,
            IsDeleted = false,
        };
        _db.Payments.Seed(payment);
        return payment;
    }

    // ── CreateDirectCheckout ──────────────────────────────────────────────────

    [Fact]
    public async Task CreateDirectCheckout_CreatesPaymentAndCheckoutUrl()
    {
        SeedStudent();
        SeedProgram();
        var sut = CreateSut();

        var result = await sut.CreateDirectCheckout(_programId, PaymentGateway.Stripe);

        Assert.Single(_db.Payments.Items);
        Assert.Equal(_enrollmentId, result.EnrollmentId);
        Assert.Equal("https://checkout.stripe.com/test", result.CheckoutUrl);
        Assert.Single(_db.Payments.Items);
    }

    [Fact]
    public async Task CreateDirectCheckout_Throws_WhenNotStudentOrInvalidProgram()
    {
        SeedStudent();
        SeedProgram(price: null);
        var sut = CreateSut();

        await Assert.ThrowsAsync<BadRequestException>(() =>
            sut.CreateDirectCheckout(_programId, PaymentGateway.Stripe));

        SeedParent();
        await Assert.ThrowsAsync<ForbiddenException>(() =>
            CreateSut(_parentId).CreateDirectCheckout(_programId, PaymentGateway.Stripe));

        SeedProgram(id: Guid.Parse("23232323-2323-2323-2323-232323232323"), price: 100_000m);
        await Assert.ThrowsAsync<NotFoundException>(() =>
            CreateSut().CreateDirectCheckout(Guid.Parse("99999999-9999-9999-9999-999999999999"), PaymentGateway.Stripe));
    }

    // ── CreateModuleRetakeCheckout ──────────────────────────────────────────────

    [Fact]
    public async Task CreateModuleRetakeCheckout_CreatesPayment()
    {
        SeedStudent();
        SeedProgram();
        var module = SeedModule();
        SeedModuleEnrollment(module);
        var sut = CreateSut();

        var result = await sut.CreateModuleRetakeCheckout(_moduleEnrollmentId, PaymentGateway.Stripe);

        Assert.Single(_db.Payments.Items);
        Assert.Equal(_moduleEnrollmentId, result.EnrollmentId);
    }

    [Fact]
    public async Task CreateModuleRetakeCheckout_Throws_WhenNotEligible()
    {
        SeedStudent();
        SeedProgram();
        var module = SeedModule(retakeFee: 0);
        SeedModuleEnrollment(module);
        var sut = CreateSut();

        await Assert.ThrowsAsync<BadRequestException>(() =>
            sut.CreateModuleRetakeCheckout(_moduleEnrollmentId, PaymentGateway.Stripe));

        SeedModuleEnrollment(module, EnrollmentStatus.Active);
        await Assert.ThrowsAsync<BadRequestException>(() =>
            sut.CreateModuleRetakeCheckout(_moduleEnrollmentId, PaymentGateway.Stripe));
    }

    // ── RequestParentPayment ──────────────────────────────────────────────────

    [Fact]
    public async Task RequestParentPayment_CreatesRequestAndEmailsParent()
    {
        SeedStudent();
        SeedParent();
        SeedProgram();
        _db.ParentStudents.Seed(new ParentStudent
        {
            Id = Guid.NewGuid(),
            ParentId = _parentId,
            StudentId = _studentId,
            IsVerified = true,
            IsDeleted = false,
        });
        var sut = CreateSut();

        await sut.RequestParentPayment(_programId, _parentId);

        Assert.Single(_db.PaymentRequests.Items);
        _emailService.Verify(e => e.SendPaymentRequestToParentEmailAsync(It.IsAny<PaymentRequestEmailDto>()), Times.Once);
        _notificationPublisher.Verify(
            n => n.PublishAsync(It.IsAny<NotificationCommand>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task RequestParentPayment_Throws_WhenLinkMissing()
    {
        SeedStudent();
        SeedParent();
        SeedProgram();
        var sut = CreateSut();

        await Assert.ThrowsAsync<BadRequestException>(() =>
            sut.RequestParentPayment(_programId, _parentId));
    }

    // ── CreateParentCheckout ──────────────────────────────────────────────────

    [Fact]
    public async Task CreateParentCheckout_CreatesPaymentFromToken()
    {
        SeedStudent();
        SeedParent();
        SeedProgram();
        _db.PaymentRequests.Seed(new PaymentRequest
        {
            Id = _paymentRequestId,
            StudentId = _studentId,
            ParentId = _parentId,
            ProgramId = _programId,
            ProgramEnrollmentId = _enrollmentId,
            Amount = 500_000m,
            Currency = "VND",
            Token = RequestToken,
            ExpiresAt = DateTime.UtcNow.AddHours(1),
            Status = PaymentRequestStatus.Pending,
            IsDeleted = false,
        });
        var sut = CreateSut(_parentId);

        var result = await sut.CreateParentCheckout(RequestToken, PaymentGateway.Stripe);

        Assert.Single(_db.Payments.Items);
        Assert.False(string.IsNullOrWhiteSpace(result.AccessToken));
        Assert.Equal(PaymentRequestStatus.Accepted, _db.PaymentRequests.Items[0].Status);
    }

    [Fact]
    public async Task CreateParentCheckout_Throws_WhenTokenInvalid()
    {
        var sut = CreateSut(_parentId);
        await Assert.ThrowsAsync<BadRequestException>(() =>
            sut.CreateParentCheckout("bad-token", PaymentGateway.Stripe));
    }

    // ── HandleStripeWebhook / CancelPayment / GetPaymentById ──────────────────

    [Fact]
    public async Task HandleStripeWebhook_Completed_ActivatesEnrollmentAndCreatesInvoice()
    {
        SeedStudent();
        SeedParent();
        SeedProgram();
        _db.ProgramEnrollments.Seed(new ProgramEnrollment
        {
            Id = _enrollmentId,
            StudentId = _studentId,
            ProgramId = _programId,
            Status = EnrollmentStatus.PendingPayment,
            IsDeleted = false,
        });
        SeedPendingPayment(programEnrollmentId: _enrollmentId, paidById: _parentId);
        var sut = CreateSut();

        await sut.HandleStripeWebhook("{}", "sig");

        var payment = _db.Payments.Items.Single();
        Assert.Equal(PaymentStatus.Success, payment.Status);
        Assert.Equal(EnrollmentStatus.Active, _db.ProgramEnrollments.Items.Single().Status);
        Assert.Single(_db.Invoices.Items);
        _emailService.Verify(e => e.SendPaymentInvoiceEmailAsync(It.IsAny<InvoiceEmailDto>()), Times.Once);
    }

    [Fact]
    public async Task HandleStripeWebhook_Expired_MarksPaymentFailed()
    {
        SeedStudent();
        SeedPendingPayment();
        var sut = CreateSut();
        _stripe
            .Setup(s => s.ParseWebhookEvent(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(("checkout.session.expired", CheckoutSessionId, null));

        await sut.HandleStripeWebhook("{}", "sig");

        Assert.Equal(PaymentStatus.Failed, _db.Payments.Items.Single().Status);
    }

    [Fact]
    public async Task CancelPayment_MarksCancelled_AndRollsBackAcceptedRequest()
    {
        SeedStudent();
        SeedProgram();
        _db.ProgramEnrollments.Seed(new ProgramEnrollment
        {
            Id = _enrollmentId,
            StudentId = _studentId,
            ProgramId = _programId,
            Status = EnrollmentStatus.PendingPayment,
            IsDeleted = false,
        });
        SeedPendingPayment(programEnrollmentId: _enrollmentId);
        _db.PaymentRequests.Seed(new PaymentRequest
        {
            Id = _paymentRequestId,
            StudentId = _studentId,
            ParentId = _parentId,
            ProgramId = _programId,
            ProgramEnrollmentId = _enrollmentId,
            Amount = 500_000m,
            Token = RequestToken,
            ExpiresAt = DateTime.UtcNow.AddHours(1),
            Status = PaymentRequestStatus.Accepted,
            IsDeleted = false,
        });
        var sut = CreateSut();

        await sut.CancelPayment(_paymentId);

        Assert.Equal(PaymentStatus.Cancelled, _db.Payments.Items.Single().Status);
        Assert.Equal(PaymentRequestStatus.Pending, _db.PaymentRequests.Items.Single().Status);
    }

    [Fact]
    public async Task CancelPayment_IsIdempotent_WhenNotPending()
    {
        SeedStudent();
        var payment = SeedPendingPayment();
        payment.Status = PaymentStatus.Success;
        var sut = CreateSut();

        await sut.CancelPayment(_paymentId);

        Assert.Equal(PaymentStatus.Success, _db.Payments.Items.Single().Status);
    }

    [Fact]
    public async Task GetPaymentById_AllowsOwnerAndManager()
    {
        SeedStudent();
        SeedUserManager();
        SeedPendingPayment();
        var sut = CreateSut();

        var owner = await sut.GetPaymentById(_paymentId);
        var manager = await CreateSut(_managerId).GetPaymentById(_paymentId);

        Assert.Equal(_paymentId, owner.Id);
        Assert.Equal(_paymentId, manager.Id);
    }

    [Fact]
    public async Task GetPaymentById_ForbidsOtherUser()
    {
        SeedStudent();
        SeedPendingPayment();
        var otherId = Guid.Parse("15151515-1515-1515-1515-151515151515");
        _db.Users.Seed(new User
        {
            Id = otherId,
            Code = "STD-002",
            Email = "other@test.com",
            Role = RoleType.Student,
            IsDeleted = false,
        });
        var sut = CreateSut(otherId);

        await Assert.ThrowsAsync<ForbiddenException>(() => sut.GetPaymentById(_paymentId));
    }

    [Fact]
    public async Task GetPaymentById_AllowsPayerAndAdmin()
    {
        SeedStudent();
        SeedParent();
        SeedPendingPayment(paidById: _parentId);
        var payerView = await CreateSut(_parentId).GetPaymentById(_paymentId);

        _db.Users.Seed(new User
        {
            Id = Guid.Parse("16161616-1616-1616-1616-161616161616"),
            Code = "ADM-001",
            Email = "admin@test.com",
            Role = RoleType.Admin,
            IsDeleted = false,
        });
        var adminView = await CreateSut(Guid.Parse("16161616-1616-1616-1616-161616161616")).GetPaymentById(_paymentId);

        Assert.Equal(_paymentId, payerView.Id);
        Assert.Equal(_paymentId, adminView.Id);
    }

    // ── RequestParentModulePayment ────────────────────────────────────────────

    [Fact]
    public async Task RequestParentModulePayment_CreatesRequestAndEmailsParent()
    {
        SeedStudent();
        SeedParent();
        SeedProgram();
        var module = SeedModule();
        SeedModuleEnrollment(module);
        _db.ParentStudents.Seed(new ParentStudent
        {
            Id = Guid.NewGuid(),
            ParentId = _parentId,
            StudentId = _studentId,
            IsVerified = true,
            IsDeleted = false,
        });
        var sut = CreateSut();

        await sut.RequestParentModulePayment(_moduleEnrollmentId, _parentId);

        Assert.Single(_db.PaymentRequests.Items);
        Assert.Equal(_moduleEnrollmentId, _db.PaymentRequests.Items[0].ModuleEnrollmentId);
        _emailService.Verify(e => e.SendPaymentRequestToParentEmailAsync(It.IsAny<PaymentRequestEmailDto>()), Times.Once);
    }

    [Fact]
    public async Task RequestParentModulePayment_Throws_WhenNotEligible()
    {
        SeedStudent();
        SeedParent();
        SeedProgram();
        var module = SeedModule();
        SeedModuleEnrollment(module);
        var sut = CreateSut();

        await Assert.ThrowsAsync<BadRequestException>(() =>
            sut.RequestParentModulePayment(_moduleEnrollmentId, _parentId));

        _db.ModuleEnrollments.Items[0].Status = EnrollmentStatus.Active;
        await Assert.ThrowsAsync<BadRequestException>(() =>
            sut.RequestParentModulePayment(_moduleEnrollmentId, _parentId));

        SeedParent();
        _db.ParentStudents.Seed(new ParentStudent
        {
            Id = Guid.NewGuid(),
            ParentId = _parentId,
            StudentId = _studentId,
            IsVerified = true,
            IsDeleted = false,
        });
        await Assert.ThrowsAsync<ForbiddenException>(() =>
            CreateSut(_parentId).RequestParentModulePayment(_moduleEnrollmentId, _parentId));
    }

    // ── CreateParentCheckout (module path) ────────────────────────────────────

    [Fact]
    public async Task CreateParentCheckout_ModuleRetake_CreatesPayment()
    {
        SeedStudent();
        SeedParent();
        SeedProgram();
        var module = SeedModule();
        SeedModuleEnrollment(module);
        _db.PaymentRequests.Seed(new PaymentRequest
        {
            Id = _paymentRequestId,
            StudentId = _studentId,
            ParentId = _parentId,
            ModuleId = _moduleId,
            ModuleEnrollmentId = _moduleEnrollmentId,
            Amount = module.RetakeFee,
            Currency = "VND",
            Token = RequestToken,
            ExpiresAt = DateTime.UtcNow.AddHours(1),
            Status = PaymentRequestStatus.Pending,
            IsDeleted = false,
        });
        var sut = CreateSut(_parentId);

        var result = await sut.CreateParentCheckout(RequestToken, PaymentGateway.Stripe);

        Assert.Single(_db.Payments.Items);
        Assert.Equal(_moduleEnrollmentId, result.EnrollmentId);
    }

    [Fact]
    public async Task CreateParentCheckout_Throws_WhenTokenExpiredOrInvalidType()
    {
        SeedStudent();
        SeedParent();
        _db.PaymentRequests.Seed(new PaymentRequest
        {
            Id = _paymentRequestId,
            StudentId = _studentId,
            ParentId = _parentId,
            Amount = 100_000m,
            Currency = "VND",
            Token = RequestToken,
            ExpiresAt = DateTime.UtcNow.AddHours(-1),
            Status = PaymentRequestStatus.Pending,
            IsDeleted = false,
        });
        var sut = CreateSut(_parentId);

        await Assert.ThrowsAsync<BadRequestException>(() =>
            sut.CreateParentCheckout(RequestToken, PaymentGateway.Stripe));

        _db.PaymentRequests.Items[0].ExpiresAt = DateTime.UtcNow.AddHours(1);
        await Assert.ThrowsAsync<BadRequestException>(() =>
            sut.CreateParentCheckout(RequestToken, PaymentGateway.Stripe));
    }

    // ── Webhook / failure / cancel (module paths) ─────────────────────────────

    [Fact]
    public async Task HandleStripeWebhook_Completed_ActivatesModuleEnrollmentAndSendsParentConfirmation()
    {
        SeedStudent();
        SeedParent();
        SeedProgram();
        var module = SeedModule();
        _db.ModuleEnrollments.Seed(new ModuleEnrollment
        {
            Id = _moduleEnrollmentId,
            StudentId = _studentId,
            ModuleId = module.Id,
            Status = EnrollmentStatus.PendingPayment,
            IsDeleted = false,
        });
        SeedPendingPayment(moduleEnrollmentId: _moduleEnrollmentId, paidById: _parentId);
        _db.PaymentRequests.Seed(new PaymentRequest
        {
            Id = _paymentRequestId,
            StudentId = _studentId,
            ParentId = _parentId,
            ModuleId = _moduleId,
            ModuleEnrollmentId = _moduleEnrollmentId,
            Amount = module.RetakeFee,
            Token = RequestToken,
            Status = PaymentRequestStatus.Accepted,
            IsDeleted = false,
        });
        var sut = CreateSut();

        await sut.HandleStripeWebhook("{}", "sig");

        Assert.Equal(EnrollmentStatus.Active, _db.ModuleEnrollments.Items.Single().Status);
        Assert.Equal(PaymentRequestStatus.Paid, _db.PaymentRequests.Items.Single().Status);
        _emailService.Verify(e => e.SendEnrollmentConfirmationEmailAsync(It.IsAny<EnrollmentConfirmationEmailDto>()), Times.Once);
    }

    [Fact]
    public async Task HandleStripeWebhook_Completed_IsIdempotent_WhenAlreadySuccess()
    {
        SeedStudent();
        var payment = SeedPendingPayment();
        payment.Status = PaymentStatus.Success;
        var sut = CreateSut();

        await sut.HandleStripeWebhook("{}", "sig");

        Assert.Equal(PaymentStatus.Success, _db.Payments.Items.Single().Status);
        Assert.Empty(_db.Invoices.Items);
    }

    [Fact]
    public async Task HandleStripeWebhook_Expired_NoPayment_DoesNotThrow()
    {
        var sut = CreateSut();
        _stripe
            .Setup(s => s.ParseWebhookEvent(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(("checkout.session.expired", "missing-session", null));

        await sut.HandleStripeWebhook("{}", "sig");

        Assert.Empty(_db.Payments.Items);
    }

    [Fact]
    public async Task HandleStripeWebhook_IgnoresUnknownEvent()
    {
        SeedStudent();
        SeedPendingPayment();
        var sut = CreateSut();
        _stripe
            .Setup(s => s.ParseWebhookEvent(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(("payment_intent.succeeded", CheckoutSessionId, null));

        await sut.HandleStripeWebhook("{}", "sig");

        Assert.Equal(PaymentStatus.Pending, _db.Payments.Items.Single().Status);
    }

    [Fact]
    public async Task HandleStripeWebhook_Expired_RollsBackAcceptedModulePaymentRequest()
    {
        SeedStudent();
        SeedParent();
        SeedProgram();
        var module = SeedModule();
        SeedPendingPayment(moduleEnrollmentId: _moduleEnrollmentId);
        _db.PaymentRequests.Seed(new PaymentRequest
        {
            Id = _paymentRequestId,
            StudentId = _studentId,
            ParentId = _parentId,
            ModuleId = _moduleId,
            ModuleEnrollmentId = _moduleEnrollmentId,
            Amount = module.RetakeFee,
            Token = RequestToken,
            Status = PaymentRequestStatus.Accepted,
            ExpiresAt = DateTime.UtcNow.AddHours(1),
            IsDeleted = false,
        });
        var sut = CreateSut();
        _stripe
            .Setup(s => s.ParseWebhookEvent(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(("checkout.session.expired", CheckoutSessionId, null));

        await sut.HandleStripeWebhook("{}", "sig");

        Assert.Equal(PaymentStatus.Failed, _db.Payments.Items.Single().Status);
        Assert.Equal(PaymentRequestStatus.Pending, _db.PaymentRequests.Items.Single().Status);
    }

    [Fact]
    public async Task CancelPayment_ModuleEnrollment_RollsBackAcceptedRequest()
    {
        SeedStudent();
        SeedParent();
        SeedProgram();
        var module = SeedModule();
        _db.ModuleEnrollments.Seed(new ModuleEnrollment
        {
            Id = _moduleEnrollmentId,
            StudentId = _studentId,
            ModuleId = module.Id,
            Status = EnrollmentStatus.PendingPayment,
            IsDeleted = false,
        });
        SeedPendingPayment(moduleEnrollmentId: _moduleEnrollmentId);
        _db.PaymentRequests.Seed(new PaymentRequest
        {
            Id = _paymentRequestId,
            StudentId = _studentId,
            ParentId = _parentId,
            ModuleId = _moduleId,
            ModuleEnrollmentId = _moduleEnrollmentId,
            Amount = module.RetakeFee,
            Token = RequestToken,
            Status = PaymentRequestStatus.Accepted,
            ExpiresAt = DateTime.UtcNow.AddHours(1),
            IsDeleted = false,
        });
        var sut = CreateSut();

        await sut.CancelPayment(_paymentId);

        Assert.Equal(PaymentStatus.Cancelled, _db.Payments.Items.Single().Status);
        Assert.Equal(PaymentRequestStatus.Pending, _db.PaymentRequests.Items.Single().Status);
    }

    [Fact]
    public async Task CreateDirectCheckout_UsesRichDescription_WhenProgramHasHtml()
    {
        SeedStudent();
        var program = SeedProgram();
        program.Description = "<p>Robotics <b>101</b></p>";
        var sut = CreateSut();

        await sut.CreateDirectCheckout(_programId, PaymentGateway.Stripe);

        _stripe.Verify(s => s.CreateCheckoutSession(
            It.IsAny<Payment>(),
            It.IsAny<string>(),
            It.Is<string?>(d => d != null && d.Contains("Robotics")),
            It.IsAny<string?>(),
            It.IsAny<string>(),
            It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task CreateModuleRetakeCheckout_Throws_WhenEnrollmentNotOwnedOrModuleMissing()
    {
        SeedStudent();
        SeedProgram();
        var module = SeedModule();
        SeedModuleEnrollment(module);
        var otherStudent = Guid.Parse("17171717-1717-1717-1717-171717171717");
        _db.Users.Seed(new User
        {
            Id = otherStudent,
            Code = "STD-003",
            Email = "other2@test.com",
            Role = RoleType.Student,
            IsDeleted = false,
        });

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            CreateSut(otherStudent).CreateModuleRetakeCheckout(_moduleEnrollmentId, PaymentGateway.Stripe));

        module.IsDeleted = true;
        await Assert.ThrowsAsync<NotFoundException>(() =>
            CreateSut().CreateModuleRetakeCheckout(_moduleEnrollmentId, PaymentGateway.Stripe));
    }

    private void SeedUserManager()
    {
        _db.Users.Seed(new User
        {
            Id = _managerId,
            Code = "MGR-001",
            Email = "mgr@test.com",
            Role = RoleType.Manager,
            IsDeleted = false,
        });
    }
}
