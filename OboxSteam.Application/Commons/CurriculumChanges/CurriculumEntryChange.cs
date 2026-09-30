namespace OboxSteam.Application.Commons.CurriculumChanges;

/// <summary>
/// One pending tracked-entity change handed from the persistence layer to the recorder.
/// <see cref="Entity"/> holds current values; <see cref="OriginalValues"/> holds the values
/// loaded from the database, keyed by CLR property name (empty for Added entries).
/// </summary>
public sealed record CurriculumEntryChange(
    object Entity,
    CurriculumEntryState State,
    IReadOnlyDictionary<string, object?> OriginalValues);
