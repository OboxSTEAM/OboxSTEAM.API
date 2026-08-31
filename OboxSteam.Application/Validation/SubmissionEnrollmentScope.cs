using OboxSteam.Domain.Entities;
using OboxSteam.Domain.Enums;
using OboxSteam.Domain.Interfaces;

namespace OboxSteam.Application.Validation;

/// <summary>
/// Scopes submissions to a program enrollment (or the Active seats of a class).
/// After chuyen-ca, source-PE rows must not appear on the new class except copies
/// that already sit on the new module enrollments.
/// </summary>
public static class SubmissionEnrollmentScope
{
    public static async Task<List<Guid>> GetModuleEnrollmentIdsForProgramEnrollmentAsync(
        IUnitOfWork unitOfWork,
        Guid programEnrollmentId)
    {
        var rows = await unitOfWork.ModuleEnrollments.GetAllAsync(
            me => me.ProgramEnrollmentId == programEnrollmentId && !me.IsDeleted);

        return rows.Select(me => me.Id).ToList();
    }

    public static async Task<List<Guid>> GetModuleEnrollmentIdsForClassAsync(
        IUnitOfWork unitOfWork,
        Guid classId)
    {
        var seats = await unitOfWork.ClassEnrollments.GetAllAsync(
            ce => ce.ClassId == classId
                  && ce.Status == ClassEnrollmentStatus.Active
                  && !ce.IsDeleted);

        var programEnrollmentIds = seats
            .Select(ce => ce.ProgramEnrollmentId)
            .Distinct()
            .ToList();

        if (programEnrollmentIds.Count == 0)
        {
            return [];
        }

        var rows = await unitOfWork.ModuleEnrollments.GetAllAsync(
            me => me.ProgramEnrollmentId.HasValue
                  && programEnrollmentIds.Contains(me.ProgramEnrollmentId.Value)
                  && !me.IsDeleted);

        return rows.Select(me => me.Id).ToList();
    }

    public static bool BelongsTo(Submission submission, IReadOnlyCollection<Guid> moduleEnrollmentIds)
        => submission.ModuleEnrollmentId.HasValue
           && moduleEnrollmentIds.Contains(submission.ModuleEnrollmentId.Value);
}
