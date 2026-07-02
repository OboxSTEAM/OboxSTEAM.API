namespace OboxSteam.Application.Interfaces;

/// <summary>Background job kind for personal highlight video processing.</summary>
public enum PersonalVideoJobKind
{
    InitialGeneration,
    OutputTrim
}

/// <summary>Exclude range on an output highlight video timeline (milliseconds).</summary>
public record OutputExcludeRange(long StartMs, long EndMs);

/// <summary>
/// A unit of background work for generating or trimming a highlight video item.
/// </summary>
public record PersonalVideoJob(
    Guid ItemId,
    PersonalVideoJobKind Kind,
    Guid ProgramId,
    Guid StudentId,
    string? StrengthDescription,
    string? ParentOutputS3Key,
    long? ParentDurationMs,
    IReadOnlyList<OutputExcludeRange>? ExcludeRanges);

/// <summary>
/// In-process queue that decouples the HTTP trigger from clip-building + MediaConvert work.
/// </summary>
public interface IPersonalVideoQueue
{
    void Enqueue(PersonalVideoJob job);

    ValueTask<PersonalVideoJob> DequeueAsync(CancellationToken cancellationToken);
}
