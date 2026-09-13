using OboxSteam.Application.Utils;
using OboxSteam.Domain.Entities;
using OboxSteam.Domain.Enums;
using OboxSteam.Domain.Interfaces;

namespace OboxSteam.Application.Validation;

/// <summary>
/// Bundle purchase fulfillment, sequential item gates, and load-limit exceptions
/// for gated items that cannot join a class yet.
/// </summary>
public static class BundleEnrollmentHelper
{
    public static bool IsOwnedStatus(EnrollmentStatus status)
        => status is EnrollmentStatus.Active or EnrollmentStatus.Completed;

    public static string PrerequisiteMessage(string previousProgramName)
        => $"Hoàn thành chương trình {previousProgramName} trước.";

    public static async Task ValidateBundlePrerequisiteAsync(
        IUnitOfWork unitOfWork,
        Guid studentId,
        Guid programId)
    {
        var previousName = await GetBlockingPreviousProgramNameAsync(unitOfWork, studentId, programId);
        if (previousName != null)
            throw ErrorHelper.BadRequest(PrerequisiteMessage(previousName));
    }

    public static Task<string?> GetBlockingPreviousProgramNameAsync(
        IUnitOfWork unitOfWork,
        Guid studentId,
        Guid programId)
        => FindBlockingPreviousProgramNameAsync(unitOfWork, studentId, programId);

    public static async Task ValidateBundlePrerequisiteForEnrollmentAsync(
        IUnitOfWork unitOfWork,
        ProgramEnrollment enrollment)
    {
        await ValidateBundlePrerequisiteAsync(unitOfWork, enrollment.StudentId, enrollment.ProgramId);
    }

    /// <summary>
    /// True when this PE counts toward the 2 in-progress program cap.
    /// Gated bundle items with no class seat do not count (they cannot enroll a class yet).
    /// </summary>
    public static async Task<bool> OccupiesProgramLoadSlotAsync(
        IUnitOfWork unitOfWork,
        ProgramEnrollment enrollment)
    {
        if (enrollment.IsDeleted)
            return false;

        if (enrollment.Status is not (EnrollmentStatus.Active or EnrollmentStatus.PendingPayment))
            return false;

        if (await IsGatedBundleItemWithoutClassAsync(unitOfWork, enrollment))
            return false;

        return true;
    }

    public static async Task ValidateUnderInProgressProgramLimitAsync(
        IUnitOfWork unitOfWork,
        Guid studentId,
        Guid? excludeEnrollmentId = null,
        int additionalSlots = 0)
    {
        var occupying = await CountOccupyingProgramSlotsAsync(unitOfWork, studentId, excludeEnrollmentId);
        if (additionalSlots == 0)
        {
            if (occupying >= ProgramEnrollmentValidator.MaxInProgressProgramsPerStudent)
                ThrowProgramLoadConflict();
            return;
        }

        if (occupying + additionalSlots > ProgramEnrollmentValidator.MaxInProgressProgramsPerStudent)
            ThrowProgramLoadConflict();
    }

    public static async Task ValidateBundleCheckoutLoadAsync(
        IUnitOfWork unitOfWork,
        Guid studentId,
        IReadOnlyList<ProgramBundleItem> items)
    {
        var occupying = await CountOccupyingProgramSlotsAsync(unitOfWork, studentId);
        var additional = await CountAdditionalOccupyingSlotsAfterPurchaseAsync(
            unitOfWork,
            studentId,
            items);
        if (occupying + additional > ProgramEnrollmentValidator.MaxInProgressProgramsPerStudent)
            ThrowProgramLoadConflict();
    }

    public static async Task<BundleEnrollment> GetOrCreatePendingBundleEnrollmentAsync(
        IUnitOfWork unitOfWork,
        Guid studentId,
        Guid bundleId)
    {
        var existing = await unitOfWork.BundleEnrollments.GetAllAsync(
            e => e.StudentId == studentId && e.BundleId == bundleId && !e.IsDeleted);

        var pending = existing.FirstOrDefault(e => e.Status == BundleEnrollmentStatus.PendingPayment);
        if (pending != null)
            return pending;

        if (existing.Any(e => e.Status is BundleEnrollmentStatus.Active or BundleEnrollmentStatus.Completed))
            throw ErrorHelper.Conflict("You already purchased this bundle.");

        var enrollment = new BundleEnrollment
        {
            StudentId = studentId,
            BundleId = bundleId,
            Status = BundleEnrollmentStatus.PendingPayment,
            ProgressPercent = 0m,
        };
        await unitOfWork.BundleEnrollments.AddAsync(enrollment);
        await unitOfWork.SaveChangesAsync();
        return enrollment;
    }

    /// <summary>
    /// Creates Active program enrollments for bundle items the student does not already own
    /// (Active/Completed). Deferred rows are left as-is. Failed/Dropped rows get a new Active PE.
    /// </summary>
    public static async Task EnsureActiveProgramEnrollmentsAsync(
        IUnitOfWork unitOfWork,
        Guid studentId,
        IReadOnlyList<ProgramBundleItem> items,
        DateTime enrolledAt)
    {
        foreach (var programId in items.Select(i => i.ProgramId).Distinct())
        {
            var enrollments = await unitOfWork.ProgramEnrollments.GetAllAsync(
                pe => pe.StudentId == studentId && pe.ProgramId == programId && !pe.IsDeleted);

            if (enrollments.Any(pe => IsOwnedStatus(pe.Status) || pe.Status == EnrollmentStatus.Deferred))
                continue;

            await unitOfWork.ProgramEnrollments.AddAsync(new ProgramEnrollment
            {
                StudentId = studentId,
                ProgramId = programId,
                Status = EnrollmentStatus.Active,
                ProgressPercent = 0m,
                EnrolledAt = enrolledAt,
            });
        }
    }

    public static async Task<decimal> RecalculateProgressPercentAsync(
        IUnitOfWork unitOfWork,
        Guid studentId,
        IReadOnlyList<ProgramBundleItem> items)
    {
        if (items.Count == 0)
            return 0m;

        decimal sum = 0;
        foreach (var item in items.OrderBy(i => i.SortOrder))
        {
            var enrollments = await unitOfWork.ProgramEnrollments.GetAllAsync(
                pe => pe.StudentId == studentId && pe.ProgramId == item.ProgramId && !pe.IsDeleted);
            var current = enrollments
                .Where(pe => pe.Status is EnrollmentStatus.Active
                    or EnrollmentStatus.Completed
                    or EnrollmentStatus.Deferred)
                .OrderByDescending(pe => pe.EnrolledAt ?? pe.CreatedAt)
                .FirstOrDefault();
            sum += current?.ProgressPercent ?? 0m;
        }

        return Math.Round(sum / items.Count, 2, MidpointRounding.AwayFromZero);
    }

    public static bool IsItemUnlocked(
        ProgramBundleItem item,
        IReadOnlyList<ProgramBundleItem> orderedItems,
        ISet<Guid> completedProgramIds)
    {
        if (!item.RequiresPreviousCompletion)
            return true;

        var previous = orderedItems
            .Where(i => i.SortOrder < item.SortOrder)
            .OrderByDescending(i => i.SortOrder)
            .FirstOrDefault();
        if (previous == null)
            return true;

        return completedProgramIds.Contains(previous.ProgramId);
    }

    /// <summary>
    /// Roadmap node: Completed first, then Locked when the sequential gate is closed,
    /// InProgress when unlocked with percent &gt; 0, otherwise Available.
    /// </summary>
    public static BundlePathwayItemStatus ResolvePathwayItemStatus(
        ProgramBundleItem item,
        IReadOnlyList<ProgramBundleItem> orderedItems,
        ISet<Guid> completedProgramIds,
        ProgramEnrollment? current)
    {
        if (current?.Status == EnrollmentStatus.Completed)
            return BundlePathwayItemStatus.Completed;

        if (!IsItemUnlocked(item, orderedItems, completedProgramIds))
            return BundlePathwayItemStatus.Locked;

        if (current != null
            && current.Status is EnrollmentStatus.Active or EnrollmentStatus.Deferred
            && current.ProgressPercent > 0)
        {
            return BundlePathwayItemStatus.InProgress;
        }

        return BundlePathwayItemStatus.Available;
    }

    public static ProgramEnrollment? SelectCurrentProgramEnrollment(
        IEnumerable<ProgramEnrollment> enrollments,
        Guid studentId,
        Guid programId)
    {
        return enrollments
            .Where(pe => pe.StudentId == studentId
                         && pe.ProgramId == programId
                         && !pe.IsDeleted
                         && pe.Status is EnrollmentStatus.Active
                             or EnrollmentStatus.Completed
                             or EnrollmentStatus.Deferred)
            .OrderByDescending(pe => pe.EnrolledAt ?? pe.CreatedAt)
            .FirstOrDefault();
    }

    public static IReadOnlyList<ProgramBundleItem> FindNewlyUnlockedItems(
        IReadOnlyList<ProgramBundleItem> orderedItems,
        ISet<Guid> completedBefore,
        ISet<Guid> completedAfter)
    {
        return orderedItems
            .Where(item =>
                !IsItemUnlocked(item, orderedItems, completedBefore)
                && IsItemUnlocked(item, orderedItems, completedAfter))
            .ToList();
    }

    public static async Task<HashSet<Guid>> GetCompletedProgramIdsAsync(IUnitOfWork unitOfWork, Guid studentId)
    {
        var enrollments = await unitOfWork.ProgramEnrollments.GetAllAsync(
            pe => pe.StudentId == studentId
                  && !pe.IsDeleted
                  && pe.Status == EnrollmentStatus.Completed);
        return enrollments.Select(pe => pe.ProgramId).ToHashSet();
    }

    public static bool AreAllItemsCompleted(
        IReadOnlyList<ProgramBundleItem> items,
        ISet<Guid> completedProgramIds)
        => items.Count > 0 && items.All(i => completedProgramIds.Contains(i.ProgramId));

    private static async Task<int> CountOccupyingProgramSlotsAsync(
        IUnitOfWork unitOfWork,
        Guid studentId,
        Guid? excludeEnrollmentId = null)
    {
        var enrollments = await unitOfWork.ProgramEnrollments.GetAllAsync(
            pe => pe.StudentId == studentId
                  && !pe.IsDeleted
                  && (!excludeEnrollmentId.HasValue || pe.Id != excludeEnrollmentId.Value));

        var occupying = 0;
        foreach (var enrollment in enrollments)
        {
            if (await OccupiesProgramLoadSlotAsync(unitOfWork, enrollment))
                occupying++;
        }

        return occupying;
    }

    private static async Task<int> CountAdditionalOccupyingSlotsAfterPurchaseAsync(
        IUnitOfWork unitOfWork,
        Guid studentId,
        IReadOnlyList<ProgramBundleItem> items)
    {
        var ordered = items.OrderBy(i => i.SortOrder).ToList();
        var allEnrollments = await unitOfWork.ProgramEnrollments.GetAllAsync(
            pe => pe.StudentId == studentId && !pe.IsDeleted);

        var completedProgramIds = allEnrollments
            .Where(pe => pe.Status == EnrollmentStatus.Completed)
            .Select(pe => pe.ProgramId)
            .ToHashSet();

        var ownedProgramIds = allEnrollments
            .Where(pe => IsOwnedStatus(pe.Status) || pe.Status == EnrollmentStatus.Deferred)
            .Select(pe => pe.ProgramId)
            .ToHashSet();

        var additional = 0;
        foreach (var item in ordered)
        {
            if (ownedProgramIds.Contains(item.ProgramId))
                continue;

            if (IsItemUnlocked(item, ordered, completedProgramIds))
                additional++;
        }

        return additional;
    }

    private static async Task<bool> IsGatedBundleItemWithoutClassAsync(
        IUnitOfWork unitOfWork,
        ProgramEnrollment enrollment)
    {
        var previousName = await FindBlockingPreviousProgramNameAsync(
            unitOfWork,
            enrollment.StudentId,
            enrollment.ProgramId);
        if (previousName == null)
            return false;

        var hasClass = await unitOfWork.ClassEnrollments.FirstOrDefaultAsync(
            ce => ce.ProgramEnrollmentId == enrollment.Id
                  && ce.Status == ClassEnrollmentStatus.Active
                  && !ce.IsDeleted);
        return hasClass == null;
    }

    private static async Task<string?> FindBlockingPreviousProgramNameAsync(
        IUnitOfWork unitOfWork,
        Guid studentId,
        Guid programId)
    {
        var bundleEnrollments = await unitOfWork.BundleEnrollments.GetAllAsync(
            e => e.StudentId == studentId
                 && !e.IsDeleted
                 && e.Status == BundleEnrollmentStatus.Active);
        if (bundleEnrollments.Count == 0)
            return null;

        var bundleIds = bundleEnrollments.Select(e => e.BundleId).ToHashSet();
        var items = (await unitOfWork.ProgramBundleItems.GetAllAsync(
                i => bundleIds.Contains(i.BundleId) && !i.IsDeleted))
            .OrderBy(i => i.SortOrder)
            .ToList();

        var gatedItems = items
            .Where(i => i.ProgramId == programId && i.RequiresPreviousCompletion)
            .ToList();
        if (gatedItems.Count == 0)
            return null;

        var completedProgramIds = (await unitOfWork.ProgramEnrollments.GetAllAsync(
                pe => pe.StudentId == studentId
                      && !pe.IsDeleted
                      && pe.Status == EnrollmentStatus.Completed))
            .Select(pe => pe.ProgramId)
            .ToHashSet();

        foreach (var item in gatedItems)
        {
            var bundleItems = items.Where(i => i.BundleId == item.BundleId).ToList();
            if (IsItemUnlocked(item, bundleItems, completedProgramIds))
                continue;

            var previous = bundleItems
                .Where(i => i.SortOrder < item.SortOrder)
                .OrderByDescending(i => i.SortOrder)
                .FirstOrDefault();
            if (previous == null)
                continue;

            var previousProgram = await unitOfWork.Programs.GetByIdAsync(previous.ProgramId);
            return previousProgram?.Name ?? "trước đó";
        }

        return null;
    }

    private static void ThrowProgramLoadConflict()
    {
        throw ErrorHelper.Conflict(
            $"Student has reached the maximum of {ProgramEnrollmentValidator.MaxInProgressProgramsPerStudent} " +
            "in-progress programs (Active or PendingPayment). " +
            "Complete or drop a program before starting another.");
    }
}
