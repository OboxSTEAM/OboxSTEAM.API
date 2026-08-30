using OboxSteam.Application.Utils;
using OboxSteam.Domain.Entities;
using OboxSteam.Domain.Enums;
using OboxSteam.Domain.Interfaces;

namespace OboxSteam.Application.Validation;

public static class ClassSeatHoldHelper
{
    public static async Task<IReadOnlyList<(Guid ClassId, Guid ProgramId)>> ReleaseExpiredHoldsAsync(
        IUnitOfWork unitOfWork,
        CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var pendingHolds = await unitOfWork.ClassEnrollments.GetAllAsync(
            ce => ce.Status == ClassEnrollmentStatus.Pending
                  && ce.HoldExpiresAt.HasValue
                  && !ce.IsDeleted);

        var expiredHolds = pendingHolds
            .Where(ce => AppDateTime.AsUtc(ce.HoldExpiresAt!.Value) <= now)
            .ToList();

        if (expiredHolds.Count == 0)
        {
            return Array.Empty<(Guid, Guid)>();
        }

        var affected = new List<(Guid ClassId, Guid ProgramId)>();
        var programEnrollmentIds = new HashSet<Guid>();

        foreach (var hold in expiredHolds)
        {
            if (await ShouldRetainExpiredHoldAsync(unitOfWork, hold.ProgramEnrollmentId))
            {
                continue;
            }

            programEnrollmentIds.Add(hold.ProgramEnrollmentId);
            hold.Status = ClassEnrollmentStatus.Withdrawn;
            hold.HoldExpiresAt = null;
            await unitOfWork.ClassEnrollments.Update(hold);

            var classEntity = await unitOfWork.Classes.GetByIdAsync(hold.ClassId);
            if (classEntity != null && !classEntity.IsDeleted)
            {
                affected.Add((hold.ClassId, classEntity.ProgramId));
            }
        }

        if (programEnrollmentIds.Count == 0)
        {
            return Array.Empty<(Guid, Guid)>();
        }

        await unitOfWork.SaveChangesAsync();

        foreach (var programEnrollmentId in programEnrollmentIds)
        {
            await PendingProgramCheckoutHelper.AbandonPendingProgramCheckoutAsync(
                unitOfWork,
                programEnrollmentId,
                classHoldAlreadyWithdrawn: true,
                cancellationToken);
        }

        return affected;
    }

    /// <summary>
    /// Keep a timed-out Pending hold when payment already succeeded (fulfillment still
    /// needs the row) or a Stripe checkout is still open for this enrollment.
    /// </summary>
    private static async Task<bool> ShouldRetainExpiredHoldAsync(
        IUnitOfWork unitOfWork,
        Guid programEnrollmentId)
    {
        var enrollment = await unitOfWork.ProgramEnrollments.GetByIdAsync(programEnrollmentId);
        if (enrollment == null || enrollment.IsDeleted)
        {
            return false;
        }

        if (enrollment.Status == EnrollmentStatus.Active)
        {
            return true;
        }

        if (enrollment.Status != EnrollmentStatus.PendingPayment)
        {
            return false;
        }

        var pendingPayments = await unitOfWork.Payments.GetAllAsync(
            p => p.ProgramEnrollmentId == programEnrollmentId
                 && p.Status == PaymentStatus.Pending
                 && !p.IsDeleted);

        return pendingPayments.Count > 0;
    }
}
