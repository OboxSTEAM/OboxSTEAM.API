using OboxSteam.Application.DTOs.MediaDTO;

namespace OboxSteam.Application.Interfaces;

/// <summary>
/// Orchestrates personal highlight video generation for a student within a Program.
/// </summary>
public interface IPersonalVideoService
{
    /// <summary>
    /// Triggers an asynchronous personal video generation job for the given student + program.
    /// Collects all tagged videos, applies the Logic Core clipping rules, submits a
    /// multi-input MediaConvert job, and returns immediately with the pending HighlightVideo record.
    /// If a job is already in progress for this student/program, returns the existing record.
    /// </summary>
    Task<HighlightVideoDto> TriggerPersonalVideoGenerationAsync(Guid programId, Guid studentId);

    /// <summary>
    /// Returns the current HighlightVideo record (status + URL) for a student/program pair.
    /// Returns null if no generation has been triggered yet.
    /// </summary>
    Task<HighlightVideoDto?> GetHighlightVideoAsync(Guid programId, Guid studentId);
}
