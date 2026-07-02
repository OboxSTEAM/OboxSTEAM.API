using OboxSteam.Application.DTOs.MediaDTO;

namespace OboxSteam.Application.Interfaces;

/// <summary>
/// Orchestrates personal highlight video generation and semi-automatic trimming for a student within a Program.
/// </summary>
public interface IPersonalVideoService
{
    /// <summary>Legacy: triggers or returns the default (no-spec) stack's latest item state.</summary>
    Task<HighlightVideoDto> TriggerPersonalVideoGenerationAsync(
        Guid programId, Guid studentId, string? strengthDescription = null);

    /// <summary>Legacy: returns the default stack's most recent item mapped to HighlightVideoDto.</summary>
    Task<HighlightVideoDto?> GetHighlightVideoAsync(Guid programId, Guid studentId);

    Task<HighlightVideoStackDto> CreateStackAsync(
        Guid programId, Guid studentId, string? strengthDescription = null);

    Task<IReadOnlyList<HighlightVideoStackDto>> GetStacksAsync(Guid programId, Guid studentId);

    Task<HighlightVideoStackDto?> GetStackAsync(Guid programId, Guid studentId, Guid stackId);

    Task<HighlightVideoItemDto> TrimItemAsync(
        Guid programId,
        Guid studentId,
        Guid stackId,
        Guid parentItemId,
        TrimHighlightVideoRequest request);

    Task DeleteItemAsync(Guid programId, Guid studentId, Guid stackId, Guid itemId);

    Task DeleteStackAsync(Guid programId, Guid studentId, Guid stackId);

    Task HandlePersonalVideoJobCompletionAsync(string jobId, bool isSuccess);

    Task ProcessGenerationAsync(PersonalVideoJob job);
}
