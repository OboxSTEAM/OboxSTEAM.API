using OboxSteam.Application.DTOs.MediaDTO;

namespace OboxSteam.Application.Interfaces;

/// <summary>
/// Orchestrates personal highlight video generation and editing (trim / add-segment)
/// for a student within a Class. All edits mutate a render-clip manifest and re-encode
/// via MediaConvert stitch jobs.
/// </summary>
public interface IPersonalVideoService
{
    /// <param name="studentId">
    /// Target student. When null, uses the authenticated user from JWT claims.
    /// </param>
    Task<HighlightVideoStackDto> CreateStackAsync(
        Guid classId, Guid? studentId = null, string? strengthDescription = null);

    /// <param name="studentId">
    /// Target student. When null, uses the authenticated user from JWT claims.
    /// </param>
    Task<IReadOnlyList<HighlightVideoStackDto>> GetStacksAsync(Guid classId, Guid? studentId = null);

    Task<HighlightVideoStackDto?> GetStackAsync(Guid stackId);

    Task<HighlightVideoItemDto> TrimItemAsync(
        Guid stackId,
        Guid parentItemId,
        TrimHighlightVideoRequest request);

    Task<HighlightVideoItemDto> AddSegmentAsync(
        Guid stackId,
        Guid parentItemId,
        AddHighlightSegmentRequest request);

    Task DeleteItemAsync(Guid stackId, Guid itemId);

    Task DeleteStackAsync(Guid stackId);

    Task HandlePersonalVideoJobCompletionAsync(string jobId, bool isSuccess);

    Task ProcessGenerationAsync(PersonalVideoJob job);
}
