using OboxSteam.Domain.Entities;
using OboxSteam.Domain.Enums;
using OboxSteam.Domain.Interfaces;

namespace OboxSteam.Application.Validation;

public static class PendingProgramCheckoutHelper
{
    public sealed record AbandonResult(bool Abandoned, Guid? ClassId, Guid ProgramId);

    /// <summary>
    /// Drops a checkout-only <see cref="EnrollmentStatus.PendingPayment"/> program enrollment:
    /// releases any seat hold, cancels open payments/requests, and soft-deletes the enrollment.
    /// </summary>
    public static async Task<AbandonResult> AbandonPendingProgramCheckoutAsync(
        IUnitOfWork unitOfWork,
        Guid programEnrollmentId,
        bool classHoldAlreadyWithdrawn = false,
        CancellationToken cancellationToken = default)
    {
        var enrollment = await unitOfWork.ProgramEnrollments.GetByIdAsync(programEnrollmentId);
        if (enrollment == null || enrollment.IsDeleted)
        {
            return new AbandonResult(false, null, Guid.Empty);
        }

        if (enrollment.Status != EnrollmentStatus.PendingPayment)
        {
            return new AbandonResult(false, null, enrollment.ProgramId);
        }

        Guid? releasedClassId = null;
        if (!classHoldAlreadyWithdrawn)
        {
            var hold = await unitOfWork.ClassEnrollments.FirstOrDefaultAsync(
                ce => ce.ProgramEnrollmentId == programEnrollmentId
                      && ce.Status == ClassEnrollmentStatus.Pending
                      && !ce.IsDeleted);

            if (hold != null)
            {
                releasedClassId = hold.ClassId;
                hold.Status = ClassEnrollmentStatus.Withdrawn;
                hold.HoldExpiresAt = null;
                await unitOfWork.ClassEnrollments.Update(hold);
            }
        }
        else
        {
            var withdrawnHold = await unitOfWork.ClassEnrollments.FirstOrDefaultAsync(
                ce => ce.ProgramEnrollmentId == programEnrollmentId
                      && ce.Status == ClassEnrollmentStatus.Withdrawn
                      && !ce.IsDeleted);

            releasedClassId = withdrawnHold?.ClassId;
        }

        var pendingPayments = await unitOfWork.Payments.GetAllAsync(
            p => p.ProgramEnrollmentId == programEnrollmentId
                 && p.Status == PaymentStatus.Pending
                 && !p.IsDeleted);

        foreach (var payment in pendingPayments)
        {
            payment.Status = PaymentStatus.Cancelled;
            await unitOfWork.Payments.Update(payment);
        }

        var openRequests = await unitOfWork.PaymentRequests.GetAllAsync(
            pr => pr.ProgramEnrollmentId == programEnrollmentId
                  && (pr.Status == PaymentRequestStatus.Pending
                      || pr.Status == PaymentRequestStatus.Accepted)
                  && !pr.IsDeleted);

        foreach (var request in openRequests)
        {
            request.Status = PaymentRequestStatus.Expired;
            await unitOfWork.PaymentRequests.Update(request);
        }

        await unitOfWork.ProgramEnrollments.SoftRemove(enrollment);
        await unitOfWork.SaveChangesAsync();

        return new AbandonResult(true, releasedClassId, enrollment.ProgramId);
    }
}
