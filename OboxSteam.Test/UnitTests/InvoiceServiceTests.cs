using Moq;
using OboxSteam.Application.Exceptions;
using OboxSteam.Application.Interfaces;
using OboxSteam.Application.Services;
using OboxSteam.Domain.Entities;
using OboxSteam.Domain.Enums;
using OboxSteam.Test.Helpers;

namespace OboxSteam.Test.UnitTests;

public sealed class InvoiceServiceTests
{
    private readonly Guid _studentId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private readonly Guid _otherStudentId = Guid.Parse("12121212-1212-1212-1212-121212121212");
    private readonly Guid _managerId = Guid.Parse("13131313-1313-1313-1313-131313131313");
    private readonly Guid _adminId = Guid.Parse("14141414-1414-1414-1414-141414141414");
    private readonly Guid _paymentId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private readonly Guid _otherPaymentId = Guid.Parse("23232323-2323-2323-2323-232323232323");
    private readonly Guid _invoiceId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private readonly Guid _otherInvoiceId = Guid.Parse("34343434-3434-3434-3434-343434343434");
    private readonly Guid _programId = Guid.Parse("44444444-4444-4444-4444-444444444444");
    private readonly Guid _programEnrollmentId = Guid.Parse("55555555-5555-5555-5555-555555555555");
    private readonly Guid _otherProgramEnrollmentId = Guid.Parse("56565656-5656-5656-5656-565656565656");

    private readonly DateTime _now = DateTime.UtcNow;
    private readonly InMemoryUnitOfWork _db = new();
    private readonly Mock<IClaimsService> _claimsService = new();

    private InvoiceService CreateSut(Guid? userId = null)
    {
        _claimsService.Setup(c => c.GetCurrentUserId).Returns(userId ?? _studentId);
        return new InvoiceService(_db, _claimsService.Object);
    }

    private void SeedUser(Guid id, RoleType role, string code)
    {
        _db.Users.Seed(new User
        {
            Id = id,
            Code = code,
            Email = $"{code.ToLower()}@test.com",
            FullName = code,
            Role = role,
            IsDeleted = false,
        });
    }

    private Invoice SeedInvoice(
        Guid? id = null,
        Guid? paymentId = null,
        Guid? issuedToId = null,
        string paymentCode = "PAY-001",
        DateTime? createdAt = null,
        bool isDeleted = false,
        Guid? programEnrollmentId = null)
    {
        var pid = paymentId ?? _paymentId;
        var enrollmentId = programEnrollmentId
            ?? (pid == _otherPaymentId ? _otherProgramEnrollmentId : _programEnrollmentId);
        var studentId = issuedToId ?? _studentId;

        var enrollment = new ProgramEnrollment
        {
            Id = enrollmentId,
            StudentId = studentId,
            ProgramId = _programId,
            Status = EnrollmentStatus.Active,
            IsDeleted = false,
        };
        _db.ProgramEnrollments.Seed(enrollment);

        var payment = new Payment
        {
            Id = pid,
            Code = paymentCode,
            StudentId = studentId,
            PaidById = studentId,
            ProgramEnrollmentId = enrollmentId,
            ProgramEnrollment = enrollment,
            Amount = 100m,
            Gateway = PaymentGateway.Stripe,
            Status = PaymentStatus.Success,
            PaidAt = _now,
            IsDeleted = false,
        };
        _db.Payments.Seed(payment);

        var invoice = new Invoice
        {
            Id = id ?? _invoiceId,
            InvoiceNumber = "INV-001",
            PaymentId = pid,
            Payment = payment,
            IssuedToId = studentId,
            BillingName = "Alice",
            BillingEmail = "alice@test.com",
            ItemDescription = "Program fee",
            SubTotal = 100m,
            TotalAmount = 100m,
            Currency = "VND",
            CreatedAt = createdAt ?? _now.AddDays(-1),
            IsDeleted = isDeleted,
        };
        _db.Invoices.Seed(invoice);
        return invoice;
    }

    [Fact]
    public async Task GetById_ReturnsInvoice_ForOwner()
    {
        SeedUser(_studentId, RoleType.Student, "STD-001");
        SeedInvoice();
        var sut = CreateSut();

        var result = await sut.GetById(_invoiceId);

        Assert.Equal(_invoiceId, result.Id);
        Assert.Equal("PAY-001", result.PaymentCode);
        Assert.Equal(_programId, result.ProgramId);
        Assert.Equal("Alice", result.BillingName);
    }

    [Fact]
    public async Task GetById_AllowsManagerAndAdmin()
    {
        SeedUser(_managerId, RoleType.Manager, "MGR-001");
        SeedUser(_adminId, RoleType.Admin, "ADM-001");
        SeedUser(_studentId, RoleType.Student, "STD-001");
        SeedInvoice(issuedToId: _studentId);

        var managerResult = await CreateSut(_managerId).GetById(_invoiceId);
        var adminResult = await CreateSut(_adminId).GetById(_invoiceId);

        Assert.Equal(_invoiceId, managerResult.Id);
        Assert.Equal(_invoiceId, adminResult.Id);
    }

    [Fact]
    public async Task GetById_ForbidsOtherStudent()
    {
        SeedUser(_studentId, RoleType.Student, "STD-001");
        SeedUser(_otherStudentId, RoleType.Student, "STD-002");
        SeedInvoice(issuedToId: _studentId);
        var sut = CreateSut(_otherStudentId);

        await Assert.ThrowsAsync<ForbiddenException>(() => sut.GetById(_invoiceId));
    }

    [Fact]
    public async Task GetById_Throws_WhenMissingOrUserMissing()
    {
        SeedUser(_studentId, RoleType.Student, "STD-001");
        var sut = CreateSut();

        await Assert.ThrowsAsync<NotFoundException>(() => sut.GetById(_invoiceId));
        await Assert.ThrowsAsync<UnauthorizedException>(() =>
            CreateSut(Guid.Parse("99999999-9999-9999-9999-999999999999")).GetById(_invoiceId));
    }

    [Fact]
    public async Task GetByPaymentId_ReturnsInvoice_ForOwner()
    {
        SeedUser(_studentId, RoleType.Student, "STD-001");
        SeedInvoice();
        var sut = CreateSut();

        var result = await sut.GetByPaymentId(_paymentId);

        Assert.Equal(_invoiceId, result.Id);
        Assert.Equal(_paymentId, result.PaymentId);
        Assert.Equal(_programId, result.ProgramId);
    }

    [Fact]
    public async Task GetByPaymentId_ForbidsOtherStudent()
    {
        SeedUser(_studentId, RoleType.Student, "STD-001");
        SeedUser(_otherStudentId, RoleType.Student, "STD-002");
        SeedInvoice(issuedToId: _studentId);
        var sut = CreateSut(_otherStudentId);

        await Assert.ThrowsAsync<ForbiddenException>(() => sut.GetByPaymentId(_paymentId));
    }

    [Fact]
    public async Task GetByPaymentId_Throws_WhenMissing()
    {
        SeedUser(_studentId, RoleType.Student, "STD-001");
        var sut = CreateSut();

        await Assert.ThrowsAsync<NotFoundException>(() => sut.GetByPaymentId(_paymentId));
    }

    [Fact]
    public async Task GetMyInvoices_ReturnsOrdered_ForCurrentUser()
    {
        SeedUser(_studentId, RoleType.Student, "STD-001");
        SeedInvoice(id: _otherInvoiceId, paymentId: _otherPaymentId, createdAt: _now.AddDays(-3));
        SeedInvoice(createdAt: _now.AddDays(-1));

        var result = await CreateSut().GetMyInvoices();

        Assert.Equal(2, result.Count);
        Assert.Equal(_invoiceId, result[0].Id);
        Assert.Equal(_programId, result[0].ProgramId);
        Assert.Equal(_otherInvoiceId, result[1].Id);
        Assert.Equal(_programId, result[1].ProgramId);
    }

    [Fact]
    public async Task GetMyInvoices_ExcludesOtherUsersAndDeleted()
    {
        SeedUser(_studentId, RoleType.Student, "STD-001");
        SeedInvoice();
        SeedInvoice(
            id: _otherInvoiceId,
            paymentId: _otherPaymentId,
            issuedToId: _otherStudentId,
            isDeleted: true);

        var result = await CreateSut().GetMyInvoices();

        Assert.Single(result);
        Assert.Equal(_invoiceId, result[0].Id);
        Assert.Equal(_programId, result[0].ProgramId);
    }

    [Fact]
    public async Task GetById_Throws_WhenProgramEnrollmentMissing()
    {
        SeedUser(_studentId, RoleType.Student, "STD-001");
        var payment = new Payment
        {
            Id = _paymentId,
            Code = "PAY-001",
            StudentId = _studentId,
            PaidById = _studentId,
            Amount = 100m,
            Gateway = PaymentGateway.Stripe,
            Status = PaymentStatus.Success,
            PaidAt = _now,
            IsDeleted = false,
        };
        _db.Payments.Seed(payment);
        _db.Invoices.Seed(new Invoice
        {
            Id = _invoiceId,
            InvoiceNumber = "INV-001",
            PaymentId = _paymentId,
            Payment = payment,
            IssuedToId = _studentId,
            BillingName = "Alice",
            BillingEmail = "alice@test.com",
            ItemDescription = "Program fee",
            SubTotal = 100m,
            TotalAmount = 100m,
            Currency = "VND",
            CreatedAt = _now,
            IsDeleted = false,
        });

        await Assert.ThrowsAsync<NotFoundException>(() => CreateSut().GetById(_invoiceId));
    }

    [Fact]
    public async Task GetMyInvoices_IncludesModuleRetakeInvoice_WithoutProgramEnrollment()
    {
        SeedUser(_studentId, RoleType.Student, "STD-001");
        var moduleId = Guid.Parse("77777777-7777-7777-7777-777777777777");
        var moduleEnrollmentId = Guid.Parse("88888888-8888-8888-8888-888888888888");
        var retakePaymentId = Guid.Parse("99999999-9999-9999-9999-999999999999");
        var retakeInvoiceId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");

        var module = new Module
        {
            Id = moduleId,
            Code = "MOD-R",
            Name = "Robotics Lab",
            ProgramId = _programId,
            ModuleType = ModuleType.Experiential,
            IsDeleted = false,
        };
        _db.Modules.Seed(module);

        var moduleEnrollment = new ModuleEnrollment
        {
            Id = moduleEnrollmentId,
            StudentId = _studentId,
            ModuleId = moduleId,
            Module = module,
            Status = EnrollmentStatus.Active,
            IsDeleted = false,
        };
        _db.ModuleEnrollments.Seed(moduleEnrollment);

        var payment = new Payment
        {
            Id = retakePaymentId,
            Code = "PAY-RETAKE",
            StudentId = _studentId,
            PaidById = _studentId,
            ModuleEnrollmentId = moduleEnrollmentId,
            ModuleEnrollment = moduleEnrollment,
            Amount = 50m,
            Gateway = PaymentGateway.Stripe,
            Status = PaymentStatus.Success,
            PaidAt = _now,
            IsDeleted = false,
        };
        _db.Payments.Seed(payment);

        _db.Invoices.Seed(new Invoice
        {
            Id = retakeInvoiceId,
            InvoiceNumber = "INV-RETAKE",
            PaymentId = retakePaymentId,
            Payment = payment,
            IssuedToId = _studentId,
            BillingName = "Alice",
            BillingEmail = "alice@test.com",
            ItemDescription = "Module Retake: Robotics Lab",
            SubTotal = 50m,
            TotalAmount = 50m,
            Currency = "VND",
            CreatedAt = _now,
            IsDeleted = false,
        });

        var result = await CreateSut().GetMyInvoices();

        Assert.Single(result);
        Assert.Equal(retakeInvoiceId, result[0].Id);
        Assert.Equal(_programId, result[0].ProgramId);
        Assert.Equal(moduleId, result[0].ModuleId);
        Assert.Contains("Module Retake", result[0].ItemDescription);
    }
}
