using OboxSteam.Domain.Enums;

namespace OboxSteam.Application.Interfaces;

/// <summary>
/// After a program enrollment's progress is recalculated: refresh bundle pathway
/// percent, notify program completion / sequential unlock, and issue a pathway
/// certificate when every item is Completed.
/// </summary>
public interface IBundleProgressService
{
    /// <summary>
    /// <paramref name="previousStatus"/> is the program enrollment status before
    /// <c>RecalculateProgramProgressAsync</c>. Completion notifications fire only
    /// on the Active → Completed transition.
    /// </summary>
    Task SyncAfterProgramProgressAsync(Guid programEnrollmentId, EnrollmentStatus previousStatus);
}
