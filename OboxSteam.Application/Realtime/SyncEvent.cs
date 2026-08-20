namespace OboxSteam.Application.Realtime;

/// <summary>
/// Ephemeral realtime hint pushed over SignalR. Never persisted, never toasted —
/// clients receive it and refetch the affected REST resource (REST stays the source of truth).
/// </summary>
public sealed class SyncEvent
{
    /// <summary>What changed, e.g. <see cref="SyncScopes.CurriculumStructureChanged"/>.</summary>
    public string Scope { get; init; } = null!;

    /// <summary>Entity category the scope refers to, e.g. "Program".</summary>
    public string EntityType { get; init; } = null!;

    /// <summary>Id of the entity instance the client should refetch around (e.g. ProgramId).</summary>
    public Guid EntityId { get; init; }

    public DateTimeOffset At { get; init; }
}
