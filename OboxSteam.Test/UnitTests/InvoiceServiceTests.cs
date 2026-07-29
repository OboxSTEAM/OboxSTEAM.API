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
    private readonly Guid _superAdminId = Guid.Parse("14141414-1414-1414-1414-141414141414");
    private readonly Guid _paymentId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private readonly Guid _otherPaymentId = Guid.Parse("23232323-2323-2323-2323-232323232323");
    private readonly Guid _invoiceId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private readonly Guid _otherInvoiceId = Guid.Parse("34343434-3434-3434-3434-343434343434");

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
        bool isDeleted = false)
    {
        var pid = paymentId ?? _paymentId;
        var payment = new Payment
        {
            Id = pid,
            Code = paymentCode,
            StudentId = issuedToId ?? _studentId,
            PaidById = issuedToId ?? _studentId,
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
            IssuedToId = issuedToId ?? _studentId,
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
        Assert.Equal("Alice", result.BillingName);
    }

    [Fact]
    public async Task GetById_AllowsManagerAndSuperAdmin()
    {
        SeedUser(_managerId, RoleType.Manager, "MGR-001");
        SeedUser(_superAdminId, RoleType.SuperAdmin, "SA-001");
        SeedUser(_studentId, RoleType.Student, "STD-001");
        SeedInvoice(issuedToId: _studentId);

        var managerResult = await CreateSut(_managerId).GetById(_invoiceId);
        var adminResult = await CreateSut(_superAdminId).GetById(_invoiceId);

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
        Assert.Equal(_otherInvoiceId, result[1].Id);
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
    }
}
