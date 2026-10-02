using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OboxSteam.Application.Exceptions;
using OboxSteam.Application.Interfaces;
using OboxSteam.Application.Services;
using OboxSteam.Application.Validation;
using OboxSteam.Domain.Entities;
using OboxSteam.Domain.Enums;
using OboxSteam.Test.Helpers;

namespace OboxSteam.Test.UnitTests;

public sealed class ClassSeatHoldServiceTests
{
    private readonly InMemoryUnitOfWork _db = new();
    private readonly FakeSyncEventPublisher _sync = new();
    private readonly Guid _studentId = Guid.NewGuid();
    private readonly Guid _classId = Guid.NewGuid();
    private readonly Program _program = new() { Id = Guid.NewGuid(), Code = "PRG-001", Name = "Robotics", Price = 100 };

    private ClassSeatHoldService CreateSut()
        => new(
            _db,
            Mock.Of<IClaimsService>(),
            Mock.Of<IProgramEnrollmentService>(),
            _sync,
            Mock.Of<IClassService>(),
            new ProgramPurchaseLifecycle(
                _db,
                Mock.Of<ICurrentTime>(),
                Mock.Of<INotificationPublisher>(),
                NullLogger<ProgramPurchaseLifecycle>.Instance),
            NullLogger<ClassSeatHoldService>.Instance);

    private (ProgramEnrollment Enrollment, ClassEnrollment Hold, Payment Payment) SeedCheckout()
    {
        var enrollment = new ProgramEnrollment
        {
            Id = Guid.NewGuid(), StudentId = _studentId, ProgramId = _program.Id, Status = EnrollmentStatus.PendingPayment,
        };
        var hold = new ClassEnrollment
        {
            Id = Guid.NewGuid(), ClassId = _classId, StudentId = _studentId, ProgramEnrollmentId = enrollment.Id,
            Status = ClassEnrollmentStatus.Pending, HoldExpiresAt = DateTime.UtcNow.AddMinutes(5),
        };
        var payment = new Payment { Id = Guid.NewGuid(), ProgramEnrollmentId = enrollment.Id, Status = PaymentStatus.Pending };
        _db.ProgramEnrollments.Seed(enrollment);
        _db.ClassEnrollments.Seed(hold);
        _db.Payments.Seed(payment);
        return (enrollment, hold, payment);
    }

    [Fact]
    public async Task EnsureProgramPurchasable_Active_KeepsCheckout()
    {
        _program.Status = ProgramStatus.Active;
        var (_, hold, payment) = SeedCheckout();

        await CreateSut().EnsureProgramPurchasableAsync(_program, _studentId);

        Assert.Equal(ClassEnrollmentStatus.Pending, hold.Status);
        Assert.Equal(PaymentStatus.Pending, payment.Status);
        Assert.Empty(_sync.Events);
    }

    [Theory]
    [InlineData(ProgramStatus.Draft)]
    [InlineData(ProgramStatus.Approved)]
    [InlineData(ProgramStatus.Inactive)]
    public async Task EnsureProgramPurchasable_NotActive_ReleasesHoldAndThrows(ProgramStatus status)
    {
        _program.Status = status;
        var (enrollment, hold, payment) = SeedCheckout();

        var ex = await Assert.ThrowsAsync<BadRequestException>(
            () => CreateSut().EnsureProgramPurchasableAsync(_program, _studentId));

        Assert.Equal(ProgramEnrollmentValidator.ProgramNotAvailableCode, ex.ErrorCode);
        Assert.Equal(ClassEnrollmentStatus.Withdrawn, hold.Status);
        Assert.Null(hold.HoldExpiresAt);
        Assert.Equal(PaymentStatus.Cancelled, payment.Status);
        Assert.True(enrollment.IsDeleted);
        Assert.NotEmpty(_sync.Events);
    }

    [Fact]
    public async Task EnsureProgramPurchasable_NotActiveWithoutCheckout_Throws()
    {
        _program.Status = ProgramStatus.Draft;

        var ex = await Assert.ThrowsAsync<BadRequestException>(
            () => CreateSut().EnsureProgramPurchasableAsync(_program, _studentId));

        Assert.Equal(ProgramEnrollmentValidator.ProgramNotAvailableCode, ex.ErrorCode);
        Assert.Empty(_sync.Events);
    }
}
