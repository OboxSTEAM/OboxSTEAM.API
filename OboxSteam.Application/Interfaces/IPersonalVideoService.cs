using OboxSteam.Application.DTOs.MediaDTO;

namespace OboxSteam.Application.Interfaces;

/// <summary>
/// Orchestrates personal highlight video generation for a student within a Program.
/// </summary>
public interface IPersonalVideoService
{
    /// <summary>
    /// Queues personal highlight video generation for a student + program.
    /// Creates or resets a <c>Processing</c> HighlightVideo record and enqueues
    /// <see cref="PersonalVideoJob"/> for <c>PersonalVideoGenerationWorker</c> (clip build +
    /// MediaConvert submit). Returns immediately; completion arrives via MediaConvert webhook.
    /// If a job is already in progress for this student/program, returns the existing record.
    /// </summary>
    /// <param name="programId">Target program.</param>
    /// <param name="studentId">Target student.</param>
    /// <param name="strengthDescription">
    /// Optional description of student strengths used to filter video segments
    /// (e.g. "Thế mạnh trong thuyết trình và đánh cờ").
    /// When non-empty, only segments where visual labels demonstrate the described strength are
    /// included (via Bedrock + Rekognition Label Detection). Requires label timelines on all
    /// tagged videos — missing data fails the background job with
    /// <c>PersonalVideoFailureReason</c>. When null or empty, standard face/voice clipping applies.
    /// </param>
    Task<HighlightVideoDto> TriggerPersonalVideoGenerationAsync(
        Guid programId, Guid studentId, string? strengthDescription = null);

    /// <summary>
    /// Returns the current HighlightVideo record (status + URL) for a student/program pair.
    /// Returns null if no generation has been triggered yet.
    /// </summary>
    Task<HighlightVideoDto?> GetHighlightVideoAsync(Guid programId, Guid studentId);

    /// <summary>
    /// Processes the webhook completion of a personal video MediaConvert job.
    /// Updates the job status and fetches the S3 URL if successful.
    /// </summary>
    Task HandlePersonalVideoJobCompletionAsync(string jobId, bool isSuccess);

    /// <summary>
    /// Background entry point: builds the clip list and submits the MediaConvert job for a
    /// previously-created <c>Processing</c> HighlightVideo record. Called by the background
    /// worker (never from the HTTP request thread). On no matching clips or any error, the
    /// record is marked <c>Failed</c> with a reason rather than throwing.
    /// </summary>
    Task ProcessGenerationAsync(PersonalVideoJob job);
}
