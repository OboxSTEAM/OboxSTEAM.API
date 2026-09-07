using OboxSteam.Domain.Entities;
using OboxSteam.Domain.Enums;
using OboxSteam.Domain.Interfaces;

namespace OboxSteam.Application.Services;

/// <summary>
/// Shared load/unlink for Invited and Accepted co-teach rows. Declined stays as history.
/// </summary>
internal static class CoTeachStaffing
{
    internal static async Task<List<ClassSessionExpert>> LoadActiveOnSessionsAsync(
        IUnitOfWork unitOfWork,
        IReadOnlyCollection<Guid> sessionIds)
    {
        if (sessionIds.Count == 0)
        {
            return [];
        }

        var ids = sessionIds as List<Guid> ?? [.. sessionIds];
        var rows = await unitOfWork.ClassSessionExperts.GetAllAsync(
            e => !e.IsDeleted
                 && ids.Contains(e.ClassSessionId)
                 && (e.Status == ClassSessionExpertStatus.Invited
                     || e.Status == ClassSessionExpertStatus.Accepted));
        return rows;
    }

    internal static async Task<List<ClassSessionExpert>> LoadActiveForExpertOnProgramsAsync(
        IUnitOfWork unitOfWork,
        Guid expertId,
        IReadOnlyCollection<Guid> programIds)
    {
        if (programIds.Count == 0)
        {
            return [];
        }

        var programs = programIds as List<Guid> ?? [.. programIds];
        var classes = await unitOfWork.Classes.GetAllAsync(
            c => programs.Contains(c.ProgramId) && !c.IsDeleted);
        var classIds = classes.Select(c => c.Id).ToList();
        if (classIds.Count == 0)
        {
            return [];
        }

        var sessions = await unitOfWork.ClassSessions.GetAllAsync(
            s => classIds.Contains(s.ClassId) && !s.IsDeleted);
        var sessionIds = sessions.Select(s => s.Id).ToList();
        var active = await LoadActiveOnSessionsAsync(unitOfWork, sessionIds);
        return active.Where(e => e.ExpertId == expertId).ToList();
    }

    internal static async Task<List<ClassSessionExpert>> LoadActiveForExpertAsync(
        IUnitOfWork unitOfWork,
        Guid expertId)
    {
        var rows = await unitOfWork.ClassSessionExperts.GetAllAsync(
            e => !e.IsDeleted
                 && e.ExpertId == expertId
                 && (e.Status == ClassSessionExpertStatus.Invited
                     || e.Status == ClassSessionExpertStatus.Accepted));
        return rows;
    }

    internal static async Task SoftRemoveAsync(
        IUnitOfWork unitOfWork,
        List<ClassSessionExpert> rows)
    {
        if (rows.Count == 0)
        {
            return;
        }

        await unitOfWork.ClassSessionExperts.SoftRemoveRange(rows);
    }
}
