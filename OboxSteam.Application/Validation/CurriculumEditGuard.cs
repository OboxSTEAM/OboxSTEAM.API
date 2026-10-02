using OboxSteam.Application.Utils;
using OboxSteam.Domain.Enums;
using OboxSteam.Domain.Interfaces;

namespace OboxSteam.Application.Validation;

/// <summary>
/// Guards program and curriculum mutations while delivery cohorts are live. Locked when any
/// class is InProgress, any Open class has Active enrollments, or a student has an open Stripe or
/// parent checkout for an Open class (paying after the edit would sell a seat on a Draft program).
/// Edits on Approved or Active programs are allowed; the change recorder revokes the approval,
/// returns framework programs to Draft, and notifies the advisor.
/// </summary>
public static class CurriculumEditGuard
{
    public const string LockedCode = "CURRICULUM_LOCKED_COHORT";

    private const string OpenCheckoutMessage =
        "Program cannot be changed while a student is completing payment for one of its classes. " +
        "Try again after the checkout finishes or expires.";

    /// <summary>True while a live cohort locks the program and its curriculum.</summary>
    public static async Task<bool> IsLockedAsync(IUnitOfWork unitOfWork, Guid programId)
        => await ResolveLockAsync(unitOfWork, programId) != CohortLock.None;

    public static Task EnsureProgramCurriculumEditableAsync(IUnitOfWork unitOfWork, Guid programId)
        => EnsureNotLockedAsync(
            unitOfWork,
            programId,
            inProgressMessage:
                "Program curriculum cannot be changed while a class is in progress. " +
                "Wait for in-progress classes to complete — curriculum changes apply to new cohorts.",
            openEnrolledMessage:
                "Program curriculum cannot be changed while an open class has enrolled students.");

    /// <summary>
    /// Blocks program metadata update and soft-delete under the same cohort lock as curriculum.
    /// </summary>
    public static Task EnsureProgramEditableAsync(IUnitOfWork unitOfWork, Guid programId)
        => EnsureNotLockedAsync(
            unitOfWork,
            programId,
            inProgressMessage:
                "Program cannot be updated or deleted while a class is in progress. " +
                "Wait for in-progress classes to complete.",
            openEnrolledMessage:
                "Program cannot be updated or deleted while an open class has enrolled students.");

    private static async Task EnsureNotLockedAsync(
        IUnitOfWork unitOfWork,
        Guid programId,
        string inProgressMessage,
        string openEnrolledMessage)
    {
        switch (await ResolveLockAsync(unitOfWork, programId))
        {
            case CohortLock.InProgress:
                throw ErrorHelper.Conflict(inProgressMessage, LockedCode);
            case CohortLock.OpenWithEnrollments:
                throw ErrorHelper.Conflict(openEnrolledMessage, LockedCode);
            case CohortLock.OpenCheckout:
                throw ErrorHelper.Conflict(OpenCheckoutMessage, LockedCode);
        }
    }

    private static async Task<CohortLock> ResolveLockAsync(IUnitOfWork unitOfWork, Guid programId)
    {
        var classes = await unitOfWork.Classes.GetAllAsync(
            c => c.ProgramId == programId
                 && !c.IsDeleted
                 && (c.Status == ClassStatus.InProgress || c.Status == ClassStatus.Open));

        if (classes.Any(c => c.Status == ClassStatus.InProgress))
        {
            return CohortLock.InProgress;
        }

        var openClassIds = classes
            .Where(c => c.Status == ClassStatus.Open)
            .Select(c => c.Id)
            .ToList();

        if (openClassIds.Count == 0)
        {
            return CohortLock.None;
        }

        var hasEnrolledStudents = unitOfWork.ClassEnrollments
            .GetQueryable()
            .Any(e => openClassIds.Contains(e.ClassId)
                      && e.Status == ClassEnrollmentStatus.Active
                      && !e.IsDeleted);
        if (hasEnrolledStudents)
        {
            return CohortLock.OpenWithEnrollments;
        }

        var now = DateTime.UtcNow;
        var heldProgramEnrollmentIds = unitOfWork.ClassEnrollments
            .GetQueryable()
            .Where(e => openClassIds.Contains(e.ClassId)
                        && e.Status == ClassEnrollmentStatus.Pending
                        && e.HoldExpiresAt != null
                        && e.HoldExpiresAt > now
                        && !e.IsDeleted)
            .Select(e => e.ProgramEnrollmentId)
            .ToList();
        if (heldProgramEnrollmentIds.Count == 0)
        {
            return CohortLock.None;
        }

        var hasOpenCheckout = unitOfWork.Payments
            .GetQueryable()
            .Any(p => p.ProgramEnrollmentId != null
                      && heldProgramEnrollmentIds.Contains(p.ProgramEnrollmentId.Value)
                      && p.Status == PaymentStatus.Pending
                      && !p.IsDeleted);

        return hasOpenCheckout ? CohortLock.OpenCheckout : CohortLock.None;
    }

    private enum CohortLock
    {
        None,
        InProgress,
        OpenWithEnrollments,
        OpenCheckout,
    }
}
