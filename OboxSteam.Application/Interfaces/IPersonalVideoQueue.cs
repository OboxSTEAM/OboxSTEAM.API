namespace OboxSteam.Application.Interfaces;

/// <summary>
/// A unit of background work for generating a student's personal highlight video.
/// Enqueued by <see cref="IPersonalVideoService.TriggerPersonalVideoGenerationAsync"/> after the
/// HighlightVideo record has been created in <c>Processing</c> state, and consumed by the
/// background worker which calls <see cref="IPersonalVideoService.ProcessGenerationAsync"/>.
/// </summary>
/// <param name="HighlightVideoId">The HighlightVideo record to update with the result.</param>
/// <param name="ProgramId">Target program.</param>
/// <param name="StudentId">Target student.</param>
/// <param name="StrengthDescription">Optional strengths filter (null = face-only).</param>
public record PersonalVideoJob(
    Guid HighlightVideoId,
    Guid ProgramId,
    Guid StudentId,
    string? StrengthDescription);

/// <summary>
/// In-process queue that decouples the (fast) HTTP trigger from the (slow) clip-building +
/// MediaConvert submission work, so the API can return <c>202 Processing</c> immediately
/// instead of blocking the request thread on Rekognition/Bedrock calls.
/// </summary>
public interface IPersonalVideoQueue
{
    /// <summary>Enqueues a generation job for background processing. Non-blocking.</summary>
    void Enqueue(PersonalVideoJob job);

    /// <summary>Awaits the next queued job. Completes when an item is available or cancelled.</summary>
    ValueTask<PersonalVideoJob> DequeueAsync(CancellationToken cancellationToken);
}
