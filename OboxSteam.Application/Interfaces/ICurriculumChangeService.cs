using OboxSteam.Application.DTOs.CurriculumChangeDTO;

namespace OboxSteam.Application.Interfaces;

public interface ICurriculumChangeService
{
    /// <param name="baseSpec"><c>lastApproval</c> (default), <c>lastSeen</c>, <c>start</c>, or <c>version:N</c>.</param>
    /// <param name="toVersion">Upper bound; defaults to the current curriculum version.</param>
    Task<CurriculumChangesDto> GetChangesAsync(Guid programId, string? baseSpec, long? toVersion);

    Task MarkSeenAsync(Guid programId, MarkCurriculumChangesSeenRequest request);
}
