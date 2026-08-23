using OboxSteam.Application.Utils;
using OboxSteam.Domain.Enums;
using OboxSteam.Domain.Interfaces;

namespace OboxSteam.Application.Validation;

/// <summary>
/// Guards program and curriculum mutations while delivery cohorts are live.
/// Locked when any class is InProgress, or any Open class has Active enrollments —
/// structural/metadata changes are meant to land between cohorts.
/// </summary>
public static class CurriculumEditGuard
{
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
        var classes = await unitOfWork.Classes.GetAllAsync(
            c => c.ProgramId == programId
                 && !c.IsDeleted
                 && (c.Status == ClassStatus.InProgress || c.Status == ClassStatus.Open));

        if (classes.Any(c => c.Status == ClassStatus.InProgress))
        {
            throw ErrorHelper.Conflict(inProgressMessage);
        }

        var openClassIds = classes
            .Where(c => c.Status == ClassStatus.Open)
            .Select(c => c.Id)
            .ToList();

        if (openClassIds.Count == 0)
        {
            return;
        }

        var hasEnrolledStudents = unitOfWork.ClassEnrollments
            .GetQueryable()
            .Any(e => openClassIds.Contains(e.ClassId)
                      && e.Status == ClassEnrollmentStatus.Active
                      && !e.IsDeleted);

        if (hasEnrolledStudents)
        {
            throw ErrorHelper.Conflict(openEnrolledMessage);
        }
    }
}
