namespace OboxSteam.Application.Interfaces;

/// <summary>Background job kind for personal highlight video processing.</summary>
public enum PersonalVideoJobKind
{
    /// <summary>Build render clips from tagged media and encode.</summary>
    InitialGeneration,

    /// <summary>Encode from the item's stored render-clip manifest (trim / add-segment).</summary>
    ManifestEncode
}

/// <summary>
/// A unit of background work for generating or editing a highlight video item.
/// </summary>
public record PersonalVideoJob(
    Guid ItemId,
    PersonalVideoJobKind Kind,
    Guid ClassId,
    Guid StudentId,
    string? StrengthDescription);

/// <summary>
/// In-process queue that decouples the HTTP trigger from clip-building + MediaConvert work.
/// </summary>
public interface IPersonalVideoQueue
{
    void Enqueue(PersonalVideoJob job);

    ValueTask<PersonalVideoJob> DequeueAsync(CancellationToken cancellationToken);
}
