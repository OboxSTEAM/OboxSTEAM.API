using OboxSteam.Domain.Entities;

namespace OboxSteam.Application.Interfaces;

public interface IClassSeatHoldService
{
    Task<IReadOnlyList<(Guid ClassId, Guid ProgramId)>> ReleaseExpiredHoldsAsync(
        CancellationToken cancellationToken = default);

    Task<(ClassEnrollment Hold, IReadOnlyList<Guid> AffectedClassIds)> CreateOrRefreshHoldAsync(
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

    Task PublishSeatsChangedAsync(Guid programId, Guid classId, CancellationToken cancellationToken = default);
}
