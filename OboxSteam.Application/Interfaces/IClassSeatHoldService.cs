using OboxSteam.Application.DTOs.PaymentDTO;
using OboxSteam.Domain.Entities;

namespace OboxSteam.Application.Interfaces;

public interface IClassSeatHoldService
{
    Task<IReadOnlyList<(Guid ClassId, Guid ProgramId)>> ReleaseExpiredHoldsAsync(
        CancellationToken cancellationToken = default);

    Task<SelectProgramClassResponseDto> SelectClassForCheckoutAsync(
        Guid programId,
        Guid classId,
        CancellationToken cancellationToken = default);

    Task<(ClassEnrollment Hold, IReadOnlyList<Guid> AffectedClassIds)> CreateOrRefreshHoldAsync(
        Guid studentId,
        ProgramEnrollment programEnrollment,
        Guid classId,
        CancellationToken cancellationToken = default);

    Task<ClassEnrollment> RequireValidHoldAsync(
        Guid studentId,
        ProgramEnrollment programEnrollment,
        Guid classId,
        CancellationToken cancellationToken = default);

    Task<(Guid? ClassId, Guid? ProgramId)> WithdrawHoldForProgramEnrollmentAsync(
        Guid programEnrollmentId,
        CancellationToken cancellationToken = default);

    Task ActivateHoldAfterPaymentAsync(
        Guid programEnrollmentId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Extends the current Pending seat hold to cover an open Stripe Checkout session.
    /// </summary>
    Task<ClassEnrollment> PinHoldForOpenCheckoutAsync(
        Guid programEnrollmentId,
        CancellationToken cancellationToken = default);

    Task PublishSeatsChangedAsync(Guid programId, Guid classId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Releases the current student's checkout hold for a program and abandons the pending enrollment.
    /// Idempotent when no hold or pending enrollment exists.
    /// </summary>
    Task ReleaseClassHoldForCheckoutAsync(
        Guid programId,
        CancellationToken cancellationToken = default);
}
