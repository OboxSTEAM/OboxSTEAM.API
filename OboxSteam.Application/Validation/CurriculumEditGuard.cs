using OboxSteam.Application.Utils;
using OboxSteam.Domain.Enums;
using OboxSteam.Domain.Interfaces;

namespace OboxSteam.Application.Validation;

/// <summary>
/// Guards program and curriculum mutations while delivery cohorts are live. Locked when any
/// class is InProgress, or any Open class has Active enrollments. Edits on Approved or Active
/// programs are allowed; the change recorder revokes the approval and notifies the advisor.
/// </summary>
public static class CurriculumEditGuard
{
    public const string LockedCode = "CURRICULUM_LOCKED_COHORT";

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

        return hasEnrolledStudents ? CohortLock.OpenWithEnrollments : CohortLock.None;
    }

    private enum CohortLock
    {
        None,
        InProgress,
        OpenWithEnrollments,
    }
}
