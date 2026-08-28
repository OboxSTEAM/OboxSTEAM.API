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
}
